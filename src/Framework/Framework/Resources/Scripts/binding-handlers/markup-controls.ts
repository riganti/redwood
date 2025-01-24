import { isDotvvmObservable, isFakeObservableObject, unmapKnockoutObservables } from '../state-manager';
import { logWarning } from '../utils/logging';
import { defineConstantProperty, isPrimitive, keys } from '../utils/objects';
import * as manager from '../viewModules/viewModuleManager';

function isCommand(value: any, prop: string) {
    return !ko.isObservable(value[prop]) && typeof value[prop] === 'function';
}

/** Wraps a function returning observable to make sure we have a single observable which we will not need to replace even if accessor returns a different instance. */
function createWrapperComputed<T>(valueAccessor: () => T,
                                  observableAccessor: () => KnockoutObservable<T> | T = valueAccessor,
                                  propertyDebugInfo: string | null = null) {
    const computed = ko.pureComputed<T>({
        read() {
            return valueAccessor();
        },
        write(value: T) {
            const val = observableAccessor();
            if (ko.isWriteableObservable(val)) {
                val(value);
            } else {
                logWarning("binding-handler", `Attempted to write to readonly property` + (!propertyDebugInfo ? `` : ` ` + propertyDebugInfo) + `.`);
            }
        }
    });
    (computed as any)["wrappedProperty"] = observableAccessor;
    Object.defineProperty(computed, "state", {
        get: () => {
            const x = observableAccessor() as any
            return (x && x.state) ?? unmapKnockoutObservables(x, true)
        }
    })
    defineConstantProperty(computed, "setState", (state: any) => {
        const x = observableAccessor() as any
        if (compileConstants.debug && typeof x.setState != "function") {
            throw new Error(`Cannot set state of property ${propertyDebugInfo}.`)
        }
        x.setState(state)
    })
    defineConstantProperty(computed, "patchState", (state: any) => {
        const x = observableAccessor() as any
        if (compileConstants.debug && typeof x.patchState != "function") {
            throw new Error(`Cannot patch state of property ${propertyDebugInfo}.`)
        }
        x.patchState(state)
    })
    defineConstantProperty(computed, "updateState", (updateFunction: (state: any) => any) => {
        const x = observableAccessor() as any
        if (compileConstants.debug && typeof x.updateState != "function") {
            throw new Error(`Cannot patch state of property ${propertyDebugInfo}.`)
        }
        x.updateState(updateFunction)
    })
    return computed;
}

/** Similar to createWrapperComputed, but makes sure the entire object tree pretends to be observables from DotVVM viewmodel
 *  -- i.e. with state, setState, patchState and updateState methods.
 *  The function assumes that the object hierarchy which needs wrapping is relatively small or updates are rare and simply replaces everything
 *  when the accessor value changes. */
function createWrapperComputedRecursive<T>(accessor: () => KnockoutObservable<T> | T,
                                           propertyDebugInfo: string | null = null) {
    return createWrapperComputed<T>(/*valueAccessor:*/ () => processValue(accessor, accessor()),
                                    /*observableAccessor:*/ accessor,
                                    propertyDebugInfo)

    function processValue(accessor: () => KnockoutObservable<unknown> | unknown, value: unknown): any {
        const unwrapped = ko.unwrap(value)
        // skip if:
        // * primitive: don't need any nested wrapping
        // * DotVVM VM observable: assume already wrapped recursively
        // * DotVVM FakeObservableObject: again, it's already wrapped recursively
        if (isPrimitive(unwrapped) || isDotvvmObservable(value) || isFakeObservableObject(unwrapped)) {
            return unwrapped
        }

        if (Array.isArray(unwrapped)) {
            return unwrapped.map((item, index) => makeConstantObservable(() => ko.unwrap(accessor() as any)?.[index], item))
        } else {
            return Object.freeze(Object.fromEntries(
                Object.entries(unwrapped as object).map(([prop, value]) =>
                    [prop, makeConstantObservable(() => ko.unwrap(accessor() as any)?.[prop], value)]
                )
            ))
        }
    }

    function makeConstantObservable(accessor: () => KnockoutObservable<unknown> | unknown, value: unknown) {
        // the value in observable is constant, we'll create new one if accessor returns new value
        // however, this process is asynchronnous, so for writes and `state`, `setState`, ... calls we call it again to be sure
        const processed = processValue(accessor, value)
        return createWrapperComputed(() => processed, accessor, propertyDebugInfo)
    }
}

function prepareViewModuleContexts(element: HTMLElement, value: any, properties: object) {
    if (compileConstants.debug && value.modules.length == 0) {
        throw new Error(`dotvvm-with-view-modules binding was used without any modules.`)
    }
    const contexts: any = {};
    for (const viewModuleName of value.modules) {
        contexts[viewModuleName] = manager.initViewModule(viewModuleName, value.viewId ?? element, element, properties);
    }
    if (typeof value.viewId !== "string") {
        if (compileConstants.debug && value.viewId != null) {
            throw new Error(`View id ${value.viewId} is invalid.`)
        }
        (element as any)[manager.viewModulesSymbol] = contexts;
    }
    return contexts;
}

ko.virtualElements.allowedBindings["dotvvm-with-control-properties"] = true;
ko.virtualElements.allowedBindings["dotvvm-with-view-modules"] = true;

export function wrapControlProperties(valueAccessor: () => any) {
    const value = valueAccessor();
    for (const prop of keys(value)) {
        if (isCommand(value, prop)) {
            const commandFunction = value[prop];
            value[prop] = createWrapperComputed(() => commandFunction);
        } else {
            value[prop] = createWrapperComputedRecursive(
                () => valueAccessor()[prop],
                compileConstants.debug ? `'${prop}' at '${valueAccessor}'` : prop);
        }
    }
    return value
}

export default {
    'dotvvm-with-control-properties': {
        init: (element: HTMLElement, valueAccessor: () => any, allBindings?: any, viewModel?: any, bindingContext?: KnockoutBindingContext) => {
            var newContext: any = { $control: wrapControlProperties(valueAccessor) }

            // we have to merge these two bindings, since knockout does not support multiple binding to change the data context
            const viewModuleBinding = allBindings.get('dotvvm-with-view-modules')
            if (viewModuleBinding)
            {
                newContext.$viewModules = prepareViewModuleContexts(element, viewModuleBinding, newContext.$control)
            }

            const innerBindingContext = bindingContext!.extend(newContext);
            ko.applyBindingsToDescendants(innerBindingContext, element);
            return { controlsDescendantBindings: true }; // do not apply binding again
        }
    },
    'dotvvm-with-view-modules': {
        init: (element: HTMLElement, valueAccessor: () => any, allBindings?: any, viewModel?: any, bindingContext?: KnockoutBindingContext) => {
            if (allBindings.has('dotvvm-with-control-properties'))
                return;

            const contexts = prepareViewModuleContexts(element, valueAccessor(), {});

            const innerBindingContext = bindingContext!.extend({ $viewModules: contexts });
            ko.applyBindingsToDescendants(innerBindingContext, element);
            return { controlsDescendantBindings: true }; // do not apply binding again
        }
    },
};
