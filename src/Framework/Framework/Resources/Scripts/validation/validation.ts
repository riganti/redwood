import * as evaluator from "../utils/evaluator"
import { validators } from './validators'
import { allErrors, detachAllErrors, ValidationError, getErrors } from "./error"
import { DotvvmEvent } from '../events'
import * as spaEvents from '../spa/events'
import { postbackHandlers } from "../postback/handlers"
import { DotvvmValidationContext, errorsSymbol } from "./common"
import { isPrimitive, keys } from "../utils/objects"
import { elementActions } from "./actions"
import { DotvvmPostbackError } from "../shared-classes"
import { getObjectTypeInfo } from "../metadata/typeMap"
import { tryCoerce } from "../metadata/coercer"
import { primitiveTypes } from "../metadata/primitiveTypes"
import { currentStateSymbol, lastSetErrorSymbol } from "../state-manager"
import { logError, logWarning } from "../utils/logging"
import { getViewModel, getViewModelObservable } from "../dotvvm-base"

type ValidationSummaryBinding = {
    target: KnockoutObservable<any>,
    includeErrorsFromChildren: boolean,
    includeErrorsFromTarget: boolean,
    hideWhenValid: boolean
}

type DotvvmValidationErrorsChangedEventArgs = Partial<PostbackOptions> & {
    readonly allErrors: ValidationError[]
}

const validationErrorsChanged = new DotvvmEvent<DotvvmValidationErrorsChangedEventArgs>("dotvvm.validation.events.validationErrorsChanged");

export const events = {
    validationErrorsChanged
};

export const globalValidationObject = {
    /** Dictionary of client-side validation rules. Add new items to this object if you want to add support for new validation rules */
    rules: validators,
    /** List of all currently active errors */
    errors: allErrors,
    events,
    /** Add the specified list of validation errors. `dotvvm.validation.addErrors([ { errorMessage: "test error", propertyPath: "/LoginForm/Name" } ])` */
    addErrors,
    /** Removes errors from the specified properties.
     *  The errors are removed recursively, so calling `dotvvm.validation.removeErrors("/")` removes all errors in the page,
     *  `dotvvm.validation.removeErrors("/Detail")` removes all errors from the object in property root.Detail */
    removeErrors
}

const createValidationHandler = (pathFunction: (context: KnockoutBindingContext) => any, pathId: string) => ({
    name: "validate",
    execute: (callback: () => Promise<PostbackCommitFunction>, options: PostbackOptions) => {
        if (pathFunction) {
            options.validationTargetPath = pathId
            // resolve target
            const context = ko.contextFor(options.sender!);
            const validationTarget = pathFunction(context);

            runClientSideValidation(validationTarget, options);

            if (allErrors.length > 0) {
                logError("validation", "Validation failed: postback aborted; errors: ", allErrors);
                return Promise.reject(new DotvvmPostbackError({ type: "handler", handlerName: "validation", message: "Validation failed" }))
            }
        }
        return callback();
    }
})

export const runClientSideValidation = (validationTarget: any, options: PostbackOptions) => {

    watchAndTriggerValidationErrorChanged(options,
        () => {
            detachAllErrors();
            const target = evaluator.unwrapComputedProperty(validationTarget);
            const path = evaluator.findPathToChildObservable(getViewModel(), target, "")!;
            validateViewModel(validationTarget, path);
        });
}

export function init() {
    postbackHandlers["validate"] = (opt) => createValidationHandler(opt.fn, opt.path);
    postbackHandlers["validate-root"] = () => createValidationHandler(c => getViewModelObservable(), "/");
    postbackHandlers["validate-this"] = () => createValidationHandler(c => c.$data, "_this");

    if (compileConstants.isSpa) {
        spaEvents.spaNavigating.subscribe(args => {
            watchAndTriggerValidationErrorChanged(args, () => {
                detachAllErrors();
            });
        });
    }

    // Validator
    ko.bindingHandlers["dotvvm-validation"] = {
        init: (element: HTMLElement, valueAccessor: () => any, allBindingsAccessor?: KnockoutAllBindingsAccessor) => {
            validationErrorsChanged.subscribe(_ => {
                applyValidatorActions(element, valueAccessor(), allBindingsAccessor!.get("dotvvm-validationOptions"));
            });
        },
        update: (element: HTMLElement, valueAccessor: () => any, allBindingsAccessor?: KnockoutAllBindingsAccessor) => {
            applyValidatorActions(element, valueAccessor(), allBindingsAccessor!.get("dotvvm-validationOptions"));
        }
    };

    // ValidationSummary
    ko.bindingHandlers["dotvvm-validationSummary"] = {
        init: (element: HTMLElement, valueAccessor: () => ValidationSummaryBinding) => {
            const binding = valueAccessor();

            validationErrorsChanged.subscribe(_ => {
                element.innerHTML = "";
                const errors = getValidationErrors(
                    binding.target,
                    binding.includeErrorsFromChildren,
                    binding.includeErrorsFromTarget
                );
                for (const error of errors) {
                    const item = document.createElement("li");
                    item.innerText = error.errorMessage;
                    element.appendChild(item);
                }
                
                if (binding.hideWhenValid) {
                    element.style.display = errors.length > 0 ? "" : "none";
                }
            });
        }
    }
}

function validateViewModel(viewModel: any, path: string): void {
    if (ko.isObservable(viewModel)) {
        viewModel = ko.unwrap(viewModel);
    }
    if (!viewModel || typeof viewModel !== "object") {
        return;
    }

    // find validation rules for the property type
    const typeId = ko.unwrap(viewModel.$type);
    let typeInfo;
    if (typeId) {
        typeInfo = getObjectTypeInfo(typeId);
    }

    // validate all properties
    for (const propertyName of keys(viewModel)) {
        if (propertyName[0] == '$') {
            continue;
        }

        const observable = viewModel[propertyName];
        if (!ko.isObservable(observable)) {
            continue;
        }

        const propertyValue = observable();
        const propertyPath = path + "/" + propertyName;
        let propertyInfo = null;

        if (typeInfo?.type !== "dynamic") {
            // run validators
            propertyInfo = typeInfo?.properties[propertyName];
            if (propertyInfo?.validationRules) {
                validateProperty(viewModel, observable, propertyValue, propertyPath, propertyInfo.validationRules);
            }
        }

        validateRecursive(observable, propertyValue, propertyInfo?.type || { type: "dynamic" }, propertyPath);
    }
}

function validateRecursive(observable: KnockoutObservable<any>, propertyValue: any, type: TypeDefinition, propertyPath: string) {
    if (compileConstants.debug && !ko.isObservable(observable)) {
        throw Error(`Property ${propertyPath} isn't a knockout observable and cannot be validated.`)
    }
    const lastSetError: CoerceResult = (observable as any)[lastSetErrorSymbol];
    if (lastSetError && lastSetError.isError) {
        ValidationError.attachIfNoErrors(lastSetError.message, propertyPath, observable);
    }
    
    if (Array.isArray(type)) {
        if (!propertyValue) return;
        let i = 0;
        for (const item of propertyValue) {
            validateRecursive(item, ko.unwrap(item), type[0], `${propertyPath}/[${i}]`);
            i++;
        }
        
    } else if (typeof type === "string") {
        if (!(type in primitiveTypes)) {
            validateViewModel(propertyValue, propertyPath);
        }

    } else if (typeof type === "object") {
        if (type.type === "dynamic") {

            if (Array.isArray(propertyValue)) {
                let i = 0;
                for (const item of propertyValue) {
                    validateRecursive(item, ko.unwrap(item), { type: "dynamic" }, `${propertyPath}/[${i}]`);
                    i++;
                }
            } else if (propertyValue && typeof propertyValue === "object") {
                if (propertyValue["$type"]) {
                    validateViewModel(propertyValue, propertyPath);
                } else {
                    for (const k of keys(propertyValue)) {
                        validateRecursive(propertyValue[k], ko.unwrap(propertyValue[k]), { type: "dynamic" }, propertyPath + "/" + k);
                    }
                }
            }

        }
    }
}

/** validates the specified property in the viewModel */
function validateProperty(viewModel: any, property: KnockoutObservable<any>, value: any, propertyPath: string, propertyRules: PropertyValidationRuleInfo[]) {
    for (const rule of propertyRules) {
        // validate the rules
        const validator = validators[rule.ruleName];
        const context: DotvvmValidationContext = {
            valueToValidate: value,
            parentViewModel: viewModel,
            parameters: rule.parameters
        };

        if (!validator.isValid(value, context, property)) {
            ValidationError.attach(rule.errorMessage, propertyPath, property);
        }
    }
}

/**
 * Gets validation errors from the passed object and its children.
 * @param targetObservable Object that is supposed to contain the errors or properties with the errors
 * @param includeErrorsFromGrandChildren Is called "IncludeErrorsFromChildren" in ValidationSummary.cs
 * @returns By default returns only errors from the viewModel's immediate children
 */
function getValidationErrors<T>(
    targetObservable: KnockoutObservable<T> | T | null,
    includeErrorsFromGrandChildren: boolean,
    includeErrorsFromTarget: boolean,
    includeErrorsFromChildren = true): ValidationError[] {

    if (!targetObservable) {
        return [];
    }

    let errors: ValidationError[] = [];

    if (includeErrorsFromTarget && ko.isObservable(targetObservable) && (targetObservable as any)[errorsSymbol] != null) {
        errors = errors.concat((targetObservable as any)[errorsSymbol]);
    }
     
    if (!includeErrorsFromChildren) {
        return errors;
    }

    const validationTarget: any = ko.unwrap(targetObservable);
    if (isPrimitive(validationTarget)) {
        return errors;
    }
    if (Array.isArray(validationTarget)) {
        for (const item of validationTarget) {
            // the next children are grandchildren
            errors = errors.concat(getValidationErrors(
                item,
                includeErrorsFromGrandChildren,
                true,
                includeErrorsFromGrandChildren));
        }
        return errors;
    }
    for (const propertyName of keys(validationTarget)) {
        if (propertyName[0] == '$') {
            continue;
        }

        const property = validationTarget[propertyName];
        if (!ko.isObservable(property)) {
            continue;
        }

        // consider nested properties to be children
        errors = errors.concat(getValidationErrors(
            property,
            includeErrorsFromGrandChildren,
            true,
            includeErrorsFromGrandChildren));
    }
    return errors;
}

export type ValidationErrorDescriptor = {
    /** Error to be displayed to the user */
    errorMessage: string
    /** Path in the view model to the annotated property, for example `/LoginPage/Name` */
    propertyPath: string
}

export type AddErrorsOptions = {
    /** Root object from which are the property paths resolved. By default it's the root view model of the page */
    root?: KnockoutObservable<any> | any
    /** When set to false, the validationErrorsChanged is not triggered. By default it's true -> the error is triggered.
     *  The validationErrorsChanged event controls the `Validator`s in the DotHTML page, so when it's not triggered, the change won't be visible. */
    triggerErrorsChanged?: false
}

export function removeErrors(...paths: string[]) {
    function pathStartsWith(prefixPath: string, path: string) {
        // normalize paths = append / to each path and remove duplicated slashes
        const normRegex = /(\/+)/g
        prefixPath = (prefixPath + "/").replace(normRegex, "/")
        path = (path + "/").replace(normRegex, "/")

        return prefixPath == path || path.startsWith(prefixPath)
    }

    let changed = false;

    const errorsCopy = Array.from(allErrors)
    errorsCopy.reverse()
    for (const e of errorsCopy) {
        if (paths.some(p => pathStartsWith(p, e.propertyPath))) {
            e.detach();
            changed = true;
        }
    }

    if (changed)
        validationErrorsChanged.trigger({ allErrors })
}


export function addErrors(errors: ValidationErrorDescriptor[], options: AddErrorsOptions = {}): void {
    const root = options.root ?? getViewModelObservable()
    for (const prop of errors) {
        const propertyPath = prop.propertyPath;
        try {
            // find the property
            const property = evaluator.traverseContext(root, propertyPath);
            ValidationError.attach(prop.errorMessage, propertyPath, property);
        } catch (err) {
            logWarning("validation", err);
        }
    }

    if (options.triggerErrorsChanged !== false && errors.length > 0) {
        validationErrorsChanged.trigger({ allErrors });
    }
}

/**
 * Adds validation errors from the server to the appropriate arrays
 */
export function showValidationErrorsFromServer(serverResponseObject: any, options: PostbackOptions) {
    watchAndTriggerValidationErrorChanged(options, () => {
        detachAllErrors()

        // add validation errors
        addErrors(serverResponseObject.modelState, {triggerErrorsChanged: false})
    });

    if (allErrors.length > 0) {
        logError("validation", "Server-side validation failed; errors: ", allErrors);
    }
}

function applyValidatorActions(
    validator: HTMLElement,
    observable: any,
    validatorOptions: any): void {

    const errors = getErrors(observable);
    const errorMessages = errors.map(v => v.errorMessage);
    for (const option of keys(validatorOptions)) {
        elementActions[option](
            validator,
            errorMessages,
            validatorOptions[option]);
    }
}

function watchAndTriggerValidationErrorChanged(options: PostbackOptions, action: () => void) {
    const originalErrorsCount = allErrors.length;
    action();

    const currentErrorsCount = allErrors.length;
    if (originalErrorsCount == 0 && currentErrorsCount == 0) {
        // no errors before, no errors now
        return;
    }

    validationErrorsChanged.trigger({ ...options, allErrors });
}
