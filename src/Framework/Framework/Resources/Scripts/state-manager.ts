/// Holds the dotvvm viewmodel

import { createArray, defineConstantProperty, isPrimitive, keys } from "./utils/objects";
import { DotvvmEvent } from "./events";
import { extendToObservableArrayIfRequired } from "./serialization/deserialize"
import { areObjectTypesEqual, formatTypeName, getObjectTypeInfo } from "./metadata/typeMap";
import { coerce } from "./metadata/coercer";
import { patchViewModel } from "./postback/updater";
import { hackInvokeNotifySubscribers } from "./utils/knockout";
import { logWarning } from "./utils/logging";
import {ValidationError} from "./validation/error";
import { errorsSymbol } from "./validation/common";


export const currentStateSymbol = Symbol("currentState")
export const notifySymbol = Symbol("notify")
export const lastSetErrorSymbol = Symbol("lastSetError")

export const internalPropCache = Symbol("internalPropCache")
export const updateSymbol = Symbol("update")
export const updatePropertySymbol = Symbol("updateProperty")

let isViewModelUpdating: boolean = false;

export function getIsViewModelUpdating() {
    return isViewModelUpdating;
}

export type UpdatableObjectExtensions<T> = {
    [notifySymbol]: (newValue: T) => void
    [currentStateSymbol]: T
    [updateSymbol]?: UpdateDispatcher<T>
}

/** Manages the consistency of DotVVM immutable ViewModel state object with the knockout observables.
 * Knockout observables are by-default updated asynchronously after a state change, but the synchronization can be forced by calling `doUpdateNow`.
 * The newState event is also called asynchronously right before the knockout observables are updated.
 * Changes from observables to state are immediate.
 */
export class StateManager<TViewModel extends { $type?: TypeDefinition }> implements DotvvmStateContainer<TViewModel> {
    /** The knockout observable containing the root objects, equivalent to `dotvvm.viewModels.root.viewModel` */
    public readonly stateObservable: DeepKnockoutObservable<TViewModel>;
    private _state: DeepReadonly<TViewModel>
    /** Returns the current  */
    public get state() {
        return this._state
    }
    private _isDirty: boolean = false;
    /** Indicates whether there is a pending update of the knockout observables. */
    public get isDirty() {
        return this._isDirty
    }
    private _currentFrameNumber : number | null = 0;

    constructor(
        initialState: DeepReadonly<TViewModel>,
        public stateUpdateEvent?: DotvvmEvent<DeepReadonly<TViewModel>>
    ) {
        this._state = coerce(initialState, initialState.$type || { type: "dynamic" })
        this.stateObservable = createWrappedObservable(initialState, (initialState as any)["$type"], () => this._state, u => this.updateState(u as any))
        this.dispatchUpdate()
    }

    private dispatchUpdate() {
        if (!this._isDirty) {
            this._isDirty = true;
            this._currentFrameNumber = window.requestAnimationFrame(this.rerender.bind(this))
        }
    }

    /** Performs a synchronous update of knockout observables with the data currently stored in `state`.
     *  Consequently, if ko.options.deferUpdates is false (default), the UI will be updated immediately.
     *  If ko.options.deferUpdates is true, the UI can be manually updated by also calling the `ko.tasks.runEarly()` function. */
    public doUpdateNow() {
        if (this._currentFrameNumber !== null)
            window.cancelAnimationFrame(this._currentFrameNumber);
        this.rerender(performance.now());
    }

    private rerender(time: number) {
        this._isDirty = false

        try {
            isViewModelUpdating = true
            ko.delaySync.pause()

            this.stateUpdateEvent?.trigger(this._state);

            (this.stateObservable as any)[notifySymbol as any](this._state)
        } finally {
            try {
                ko.delaySync.resume()
            } finally {
                isViewModelUpdating = false
            }
        }
        //logInfoVerbose("New state dispatched, t = ", performance.now() - time, "; t_cpu = ", performance.now() - realStart);
    }

    /** Sets a new view model state, after checking its type compatibility and possibly performing implicit conversions.
     *  Only the changed objects are re-checked and updated in the knockout observables.
     *  It is therefore recommended to clone only the changed parts of the view model using the `{... x, ChangedProp: 1 }` syntax.
     *  In the rarely occuring complex cases where this is difficult, you can use `structuredClone` to obtain a writable clone of some part of the viewmodel.
     * 
     *  @throws CoerceError if the new state has incompatible type.
     *  @returns The type-coerced version of the new state. */
    public setState(newState: DeepReadonly<TViewModel>): DeepReadonly<TViewModel> {
        if (compileConstants.debug && newState == null) throw new Error("State can't be null or undefined.")
        if (newState === this._state) return newState

        const type = newState.$type || this._state.$type

        const coercionResult = coerce(newState, type!, this._state)

        this.dispatchUpdate();
        return this._state = coercionResult
    }

    /** Applies a patch to the current view model state.
     *  @throws CoerceError if the new state has incompatible type.
     *  @returns The type-coerced version of the new state. */
    public patchState(patch: DeepReadonly<DeepPartial<TViewModel>>): DeepReadonly<TViewModel> {
        return this.setState(patchViewModel(this._state, patch))
    }

    /** Updates the view model state using the provided `State => State` function.
     *  @throws CoerceError if the new state has incompatible type.
     *  @returns The type-coerced version of the new state.  */
    public updateState(updater: StateUpdate<TViewModel>) {
        return this.setState(updater(this._state))
    }
    /** @deprecated Use updateState method instead */
    public update: UpdateDispatcher<TViewModel> = this.updateState;
}

class FakeObservableObject<T extends object> implements UpdatableObjectExtensions<T> {
    public [currentStateSymbol]!: T
    public [errorsSymbol]!: Array<ValidationError>
    public [updateSymbol]!: UpdateDispatcher<T>
    public [notifySymbol](newValue: T) {
        console.assert(newValue)
        this[currentStateSymbol] = newValue

        const c = this[internalPropCache]
        for (const p of keys(c)) {
            const observable = c[p]
            if (observable) {
                observable[notifySymbol]((newValue as any)[p])
            }
        }
    }
    public [internalPropCache]!: { [name: string]: (KnockoutObservable<any> & UpdatableObjectExtensions<any>) | null }

    public [updatePropertySymbol](propName: keyof DeepReadonly<T>, valUpdate: StateUpdate<any>) {
        this[updateSymbol](vm => {
            if(vm==null)
                return vm
            const newValue = valUpdate(vm[propName])
            if (vm[propName] === newValue)
                return vm
            return Object.freeze({ ...vm, [propName]: newValue }) as any
        })
    }
    constructor(initialValue: T, getter: () => DeepReadonly<T> | undefined, updater: UpdateDispatcher<T>, typeId: TypeDefinition, typeInfo: ObjectTypeMetadata | DynamicTypeMetadata | undefined, additionalProperties: string[]) {
        Object.defineProperties(this, { // define the internals as non-enumerable
            [updateSymbol]: { value: updater },
            [currentStateSymbol]: { value: initialValue, writable: true },
            [errorsSymbol]: { value: [] },
            [internalPropCache]: { value: {} }
        })

        const props = (typeInfo?.type == "object") ? typeInfo.properties : {};

        for (const p of keys(props).concat(additionalProperties)) {
            this[internalPropCache][p] = null

            Object.defineProperty(this, p, {
                enumerable: true,
                get() {
                    const cached = this[internalPropCache][p]
                    if (cached) return cached

                    const currentState = this[currentStateSymbol]
                    const newObs = createWrappedObservable(
                        currentState[p],
                        props[p]?.type,
                        () => (getter() as any)?.[p],
                        u => this[updatePropertySymbol](p, u)
                    )

                    const isDynamic = typeId === undefined || (typeId as any)?.type === "dynamic";
                    if (typeInfo && p in props) {
                        const clientExtenders = props[p].clientExtenders;
                        if (clientExtenders) {
                            for (const e of clientExtenders) {
                                (ko.extenders as any)[e.name](newObs, e.parameter)
                            }
                        }
                    } else if (!isDynamic && p.indexOf("$") !== 0) {
                        logWarning("state-manager", `Unknown property '${p}' set on an object of type ${formatTypeName(typeId)}.`);
                    }

                    this[internalPropCache][p] = newObs
                    return newObs
                }
            })
        }
        Object.seal(this)
    }
}


export function isDotvvmObservable(obj: any): obj is DotvvmObservable<any> {
    return obj?.[notifySymbol] && ko.isObservable(obj)
}

export function isFakeObservableObject(obj: any): obj is FakeObservableObject<any> {
    return obj instanceof FakeObservableObject
}

/**
 * Recursively unwraps knockout observables from the object / array hierarchy. When nothing needs to be unwrapped, the original object is returned.
 * @param allowStateUnwrap Allows accessing [currentStateSymbol], which makes it faster, but doesn't register in the knockout dependency tracker
*/
export function unmapKnockoutObservables(viewModel: any, allowStateUnwrap: boolean = false): any {
    const value = ko.unwrap(viewModel)

    if (isPrimitive(value)) {
        return value
    }

    if (value instanceof Date) {
        // return serializeDate(value)
        return value
    }

    if (allowStateUnwrap && currentStateSymbol in value) {
        return value[currentStateSymbol]
    }

    if (value instanceof Array) {
        let result: any = null
        for (let i = 0; i < value.length; i++) {
            const unwrappedItem = unmapKnockoutObservables(ko.unwrap(value[i]), allowStateUnwrap)
            if (unwrappedItem !== value[i]) {
                result ??= [...value]
                result[i] = unwrappedItem
            }
        }
        return result ?? value
    }

    let result: any = null;
    for (const prop of keys(value)) {
        const v = ko.unwrap(value[prop])
        if (typeof v != "function") {
            const unwrappedProp = unmapKnockoutObservables(v, allowStateUnwrap)
            if (unwrappedProp !== value[prop]) {
                result ??= { ...value }
                result[prop] = unwrappedProp
            }
        }
    }
    return result ?? value
}

function createObservableObject<T extends object>(initialObject: T, typeHint: TypeDefinition | undefined, getter: () => DeepReadonly<T> | undefined, update: ((updater: StateUpdate<any>) => void)) {
    const typeId = (initialObject as any)["$type"] || typeHint
    let typeInfo;
    if (typeId && !(typeId.hasOwnProperty("type") && typeId["type"] === "dynamic")) {
        typeInfo = getObjectTypeInfo(typeId)
    }

    const pSet = new Set(keys((typeInfo?.type === "object") ? typeInfo.properties : {}));
    const additionalProperties = keys(initialObject).filter(p => !pSet.has(p))

    return new FakeObservableObject(initialObject, getter, update, typeId, typeInfo, additionalProperties) as FakeObservableObject<T> & DeepKnockoutObservableObject<T>
}

/** Informs that we cloned an ko.observable, so updating it won't work */
function logObservableCloneWarning(value: any) {
    function findClonedObservable(value: any, path: string): [string, any] | undefined {
        // find observable not created by dotvvm
        if (!value[notifySymbol] && ko.isObservable(value)) {
            return [path, value]
        }
        value = ko.unwrap(value)
        if (isPrimitive(value)) return;
        if (value instanceof Array) {
            for (let i = 0; i < value.length; i++) {
                const result = findClonedObservable(value[i], path + "/" + i)
                if (result) return result
            }
        }
        if (typeof value == "object") {
            for (const p of keys(value)) {
                const result = findClonedObservable(value[p], path + "/" + p)
                if (result) return result
            }
        }
    }

    const foundObservable = findClonedObservable(value, "")
    if (foundObservable) {
        logWarning("state-manager", `Replacing old knockout observable with a new one, just because it is not created by DotVVM. Please do not assign objects with knockout observables into the knockout tree directly. Observable is at ${foundObservable[0]}, value =`, unmapKnockoutObservables(foundObservable[1], true))
    }
}

function createWrappedObservable<T>(initialValue: DeepReadonly<T>, typeHint: TypeDefinition | undefined, getter: () => DeepReadonly<T> | undefined, updater: UpdateDispatcher<T>): DeepKnockoutObservable<T> {

    let isUpdating = false

    function observableValidator(this: KnockoutObservable<T>, newValue: any): any {
        if (isUpdating) return { newValue, notifySubscribers: false }
        updatedObservable = true

        try {
            const notifySubscribers = (this as any)[lastSetErrorSymbol];
            (this as any)[lastSetErrorSymbol] = void 0;

            const unmappedValue = unmapKnockoutObservables(newValue);
            if (compileConstants.debug && unmappedValue !== newValue) {
                logObservableCloneWarning(newValue)
            }

            const oldValue = obs[currentStateSymbol];
            const coerceResult = coerce(unmappedValue, typeHint || { type: "dynamic" }, oldValue);

            updater(_ => coerceResult);
            const result = notifyCore(coerceResult, oldValue, true);

            return { newValue: result!.newContents, notifySubscribers };

        } catch (err) {
            (this as any)[lastSetErrorSymbol] = err;
            hackInvokeNotifySubscribers(this);
            logWarning("state-manager", `Cannot update observable to ${newValue}:`, err)
            throw err
        }
    }

    const obs = initialValue instanceof Array ? ko.observableArray([], observableValidator) : ko.observable(null, observableValidator) as any
    let updatedObservable = false

    function notify(newVal: any) {
        const currentValue = obs[currentStateSymbol]

        if (newVal === currentValue) {
            return
        }

        const observableWasSetFromOutside = updatedObservable
        updatedObservable = false

        obs[lastSetErrorSymbol] = void 0;
        obs[currentStateSymbol] = newVal

        const result = notifyCore(newVal, currentValue, observableWasSetFromOutside);
        if (result) {
            try {
                isUpdating = true
                obs(result.newContents)
            }
            finally {
                isUpdating = false
            }
        }
    }

    function notifyCore(newVal: any, currentValue: any, observableWasSetFromOutside: boolean): { newContents: any } | undefined {
        let newContents
        const oldContents = obs.peek()
        if (isPrimitive(newVal) || newVal instanceof Date) {
            // primitive value
            newContents = newVal
        }
        else if (newVal instanceof Array) {
            extendToObservableArrayIfRequired(obs)

            // when the observable is updated from the outside, we have to rebuild it to make sure that it contains
            // notifiable observables
            // otherwise, we want to skip the big update whenever possible - Knockout tends to update everything in the DOM when
            // we update the observableArray
            const skipUpdate = !observableWasSetFromOutside && oldContents instanceof Array && oldContents.length == newVal.length

            if (!skipUpdate) {
                const t: KnockoutObservableArray<any> = obs as any
                // take at most newVal.length from the old value
                newContents = oldContents instanceof Array ? oldContents.slice(0, newVal.length) : []
                // then append (potential) new values into the array
                for (let index = 0; index < newVal.length; index++) {
                    if (isDotvvmObservable(newContents[index])) {
                        continue
                    }
                    const itemUpdater = (update: any) => updater((viewModelArray: any) => {
                        if (viewModelArray == null || viewModelArray.length <= index) {
                            // the item or the array does not exist anymore
                            return viewModelArray
                        }
                        const newArray = [...viewModelArray]
                        newArray[index] = update(viewModelArray[index])
                        return Object.freeze(newArray) as any
                    })
                    newContents[index] = createWrappedObservable(
                        newVal[index],
                        Array.isArray(typeHint) ? typeHint[0] : void 0,
                        () => (getter() as any[])?.[index],
                        itemUpdater
                    )
                }
            }
            else {
                newContents = oldContents
            }

            // notify child objects
            for (let index = 0; index < newContents.length; index++) {
                newContents[index][notifySymbol as any](newVal[index])
            }

            if (skipUpdate) {
                return
            }
        }
        else if (!observableWasSetFromOutside && oldContents && oldContents[notifySymbol] && areObjectTypesEqual(currentValue, newVal)) {
            // smart object, supports the notification by itself
            oldContents[notifySymbol as any](newVal)

            // don't update the observable itself (the object itself doesn't change, only its properties)
            return
        }
        else {
            // create new object and replace
            newContents = createObservableObject(newVal, typeHint, getter, updater)
        }

        // return a result indicating that the observable needs to be set
        return { newContents };
    }

    obs[notifySymbol] = notify
    notify(initialValue)

    Object.defineProperty(obs, "state", {
        get: getter
    });
    defineConstantProperty(obs, "patchState", (patch: any) => {
            updater(state => patchViewModel(state, patch))
        });
    defineConstantProperty(obs, "setState", (newState: any) => {
        updater(_ => newState);
    })
    defineConstantProperty(obs, "updateState", updater)
    defineConstantProperty(obs, "updater", updater)
    return obs
}

