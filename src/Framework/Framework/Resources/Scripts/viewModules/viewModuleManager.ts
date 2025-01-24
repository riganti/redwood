import { getViewModelObservable } from "../dotvvm-base";
import { serialize } from "../serialization/serialize";
import { unmapKnockoutObservables } from "../state-manager";
import { debugQuoteString } from "../utils/logging";
import { keys } from "../utils/objects";

const registeredModules: { [name: string]: ModuleHandler } = {};

type ModuleCommand = (...args: any) => Promise<unknown>;

export const viewModulesSymbol = Symbol("viewModules");

export function registerViewModule(name: string, moduleObject: any) {
    if (compileConstants.debug && name == null) { throw new Error("Parameter name has to have a value"); }
    if (compileConstants.debug && moduleObject == null) { throw new Error("Parameter moduleObject has to have a value"); }

    //If multiple views on the same page use the module, we only want to register it the first time,
    //the other views then get initialized on their own
    if (!registeredModules[name]) {
        registeredModules[name] = new ModuleHandler(name, moduleObject);
    }
}

export function registerViewModules(modules: { [name: string]: any }) {
    for (const moduleName of keys(modules)) {
        const moduleObject = modules[moduleName];
        registerViewModule(moduleName, moduleObject);
    }
}

export function initViewModule(name: string, viewIdOrElement: string | HTMLElement, rootElement: HTMLElement, properties: object = {}): ModuleContext {
    if (compileConstants.debug && rootElement == null) { throw new Error("rootElement has to have a value"); }

    const handler = ensureModuleHandler(name);
    setupModuleDisposeHandlers(viewIdOrElement, name, rootElement);

    if (typeof viewIdOrElement === "string" && handler.contexts[viewIdOrElement]) {
        // contexts with the same viewId are shared
        const context = handler.contexts[viewIdOrElement];
        context.elements.push(rootElement);
        return context;
    }

    if (compileConstants.debug && (!("default" in handler.module) || typeof handler.module.default !== "function")) {
        throw new Error(`The module ${name} referenced in the @js directive must have a default export that is a function.`);
    }

    const context = new ModuleContext(
        name,
        [rootElement],
        properties,
        rootElement == document.body ? getViewModelObservable() : ko.contextFor(rootElement)?.$rawData
    );
    const moduleInstance = createModuleInstance(handler.module.default, context);
    context.module = moduleInstance;
    Object.freeze(context);

    if (typeof viewIdOrElement === "string") {
        handler.contexts[viewIdOrElement] = context;
    }
    
    return context;
}

function createModuleInstance(fn: Function, ...args: any) {
    return fn(...args);
}

function* getModules(viewIdOrElement: string | HTMLElement) {
    for (let moduleName of keys(registeredModules)) {
        const context = tryFindViewModuleContext(viewIdOrElement, moduleName);
        if (!(context && context.module)) continue;
        yield context
    }
}

export function callViewModuleCommand(viewIdOrElement: string | HTMLElement, commandName: string, args: any[], allowAsync: boolean = true) {
    if (compileConstants.debug && commandName == null) { throw new Error("commandName has to have a value"); }
    if (compileConstants.debug && !(args instanceof Array)) { throw new Error("args must be an array"); }

    const foundModules: ModuleContext[] = [];

    for (let context of getModules(viewIdOrElement)) {
        if (commandName in context.module && typeof context.module[commandName] === "function") {
            foundModules.push(context);
        }
    }

    if (compileConstants.debug && !foundModules.length) {
        throw new Error(`Command ${debugQuoteString(commandName)} could not be found in any of the imported modules in view ${viewIdOrElement}.`);
    }

    if (compileConstants.debug && foundModules.length > 1) {
        throw new Error(`Conflict: There were multiple commands named ${debugQuoteString(commandName)} the in imported modules in view ${viewIdOrElement}. Check modules: ${foundModules.map(m => m.moduleName).join(', ')}.`);
    }
    if (foundModules.length != 1) {
        // production short check
        throw new Error("unique command not found")
    }

    try {
        var result = foundModules[0].module[commandName](...args.map(v => serialize(v)));
        if (!allowAsync && result instanceof Promise) {
            throw compileConstants.debug ? `Command returned Promise even though it was called through _js.Invoke(${JSON.stringify(commandName)}, ...). Use the _js.InvokeAsync method to call commands which (may) return a promise.` : "Command returned Promise";
        }
        return result
    }
    catch (e: unknown) {
        if (compileConstants.debug) {
            throw new Error(`While executing command ${debugQuoteString(commandName)}(${args.map(v => JSON.stringify(serialize(v)))}), an error occurred. ${e}`);
        }
        else {
            throw e
        }
    }
}

const globalComponent: { [key: string]: DotvvmJsComponentFactory } = {}

export function findComponent(
    viewIdOrElement: null | string | HTMLElement,
    name: string
): [ModuleContext | null, DotvvmJsComponentFactory] {
    if (viewIdOrElement != null) {
        for (const context of getModules(viewIdOrElement)) {
            if (name in (context.module.$controls ?? {})) {
                return [context, context.module.$controls[name]]
            }
        }
    }
    if (name in globalComponent)
        return [null, globalComponent[name]]
    if (compileConstants.debug) {
        if (viewIdOrElement == null) {
            throw Error(`Cannot find a global JsComponent ${name}. Make sure it is registered using dotvvm.registerGlobalComponent(${JSON.stringify(name)}, ...)`)
        } else {
            const moduleNames = [...getModules(viewIdOrElement)].map(m => m.moduleName)
            throw Error(`Cannot find a JsComponent ${name} in modules ${moduleNames.join(", ")}, or the global registry. Make sure it is exported as $controls: { ${JSON.stringify(name)}: ... }`)
        }
    }
    else {
        throw Error("Cannot find JsComponent " + name)
    }
}

/** Registers a component to be used in any JsComponent, independent of view modules */
export function registerGlobalComponent(name: string, c: DotvvmJsComponentFactory) {
    if (name in globalComponent)
        throw new Error(`JsComponent ${name} is already registered`)
    globalComponent[name] = c
}

function setupModuleDisposeHandlers(viewIdOrElement: string | HTMLElement, name: string, rootElement: HTMLElement) {
    function elementDisposeCallback() {
        disposeModule(viewIdOrElement, name, rootElement);
        ko.utils.domNodeDisposal.removeDisposeCallback(rootElement, elementDisposeCallback);
    }
    ko.utils.domNodeDisposal.addDisposeCallback(rootElement, elementDisposeCallback);
}

function disposeModule(viewIdOrElement: string | HTMLElement, name: string, rootElement: HTMLElement) {
    const context = ensureViewModuleContext(viewIdOrElement, name);

    const index = context.elements.indexOf(rootElement);
    if (compileConstants.debug && index < 0) {
        throw new Error(`Cannot dispose module on a root element ${viewIdOrElement}. It has already been disposed.`);
    }
    context.elements.splice(index, 1);

    if (!context.elements.length) {
        callIfDefined(context.module, '$dispose', context);
        
        if (typeof viewIdOrElement === "string") {
            const handler = ensureModuleHandler(name);
            delete handler.contexts[viewIdOrElement];
        }
    }
}

export function registerNamedCommand(viewIdOrElement: string | HTMLElement, commandName: string, command: ModuleCommand, rootElement: HTMLElement) {
    for (const moduleName of keys(registeredModules)) {
        const context = tryFindViewModuleContext(viewIdOrElement, moduleName);
        if (context) {
            context.registerNamedCommand(commandName, command);
        }
    }
    setupNamedCommandDisposeHandlers(viewIdOrElement, commandName, rootElement);
}

function setupNamedCommandDisposeHandlers(viewIdOrElement: string | HTMLElement, name: string, rootElement: HTMLElement) {
    function elementDisposeCallback() {
        unregisterNamedCommand(viewIdOrElement, name);
        ko.utils.domNodeDisposal.removeDisposeCallback(rootElement, elementDisposeCallback);
    }
    ko.utils.domNodeDisposal.addDisposeCallback(rootElement, elementDisposeCallback);
}

export function unregisterNamedCommand(viewIdOrElement: string | HTMLElement, commandName: string) {
    for (const moduleName of keys(registeredModules)) {
        var context = tryFindViewModuleContext(viewIdOrElement, moduleName);
        if (context) {
            context.unregisterNamedCommand(commandName);
        }
    }
}

function tryFindViewModuleContext(viewIdOrElement: string | HTMLElement, name: string): ModuleContext | undefined {
    if (compileConstants.debug && viewIdOrElement == null) {
        throw new Error("viewIdOrElement is required.");
    }
    if (typeof viewIdOrElement === "string") {
        const handler = ensureModuleHandler(name);
        return handler.contexts[viewIdOrElement];
    } else {
        let contexts = (viewIdOrElement as any)[viewModulesSymbol];
        if (contexts) {
            return contexts[name];
        } else {
            let contexts = ko.contextFor(viewIdOrElement);
            while (contexts && !("$viewModules" in contexts)) {
                contexts = contexts.$parentContext;
            }
            return contexts["$viewModules"] && contexts["$viewModules"][name];
        }
    }
}

function ensureViewModuleContext(viewIdOrElement: string | HTMLElement, name: string): ModuleContext {
    const context = tryFindViewModuleContext(viewIdOrElement, name);
    if (compileConstants.debug && !context) {
        throw new Error('Module ' + name + 'has not been initialized for view ' + viewIdOrElement + ', or the view has been disposed');
    }
    return context!;
}

function ensureModuleHandler(name: string): ModuleHandler {
    if (name == null) { throw new Error("name has to have a value"); }

    const handler = registeredModules[name];
    if (compileConstants.debug && !handler) {
        throw new Error('Could not find module ' + name + '. Module is not registered, or has been disposed.');
    }
    return handler;
}

function callIfDefined(module: any, name: string, ...args: any[]) {
    if(!module) return; 
    if (typeof module[name] === 'function') {
        module[name](...args);
    }
}

class ModuleHandler {
    public contexts: { [viewId: string]: ModuleContext } = {};
    constructor(public readonly name: string, public readonly module: any) {
    }
}

function mapCommandResult(result: any) {
    if (typeof result.then == 'function')
        return result.then(mapCommandResult)
    if ("commandResult" in result && typeof result.postbackId == 'number')
        return result.commandResult
    return result
}

export class ModuleContext implements DotvvmModuleContext {
    public readonly namedCommands: { [name: string]: (...args: any[]) => Promise<any> } = {};
    public module: any;
    public setState: (state: any) => void;
    public patchState: (state: any) => void;
    public updateState: (updateFunction: StateUpdate<any>) => void;
    public state: any;
    
    constructor(
        public readonly moduleName: string,
        public readonly elements: HTMLElement[],
        public readonly properties: { [name: string]: any },
        viewModel: DotvvmObservable<any>) {

        this.setState = viewModel?.setState
        this.patchState = viewModel?.patchState
        this.updateState = viewModel?.updateState
        Object.defineProperty(this, "state", {
            get: () => viewModel.state
        })
    }
    
    public registerNamedCommand = (name: string, command: (...args: any[]) => Promise<any>) => {
        if (compileConstants.debug && (name == null || name == '' || typeof name != 'string')) {
            throw new Error(`Command name=${debugQuoteString(name)} is empty or invalid.`)
        }
        if (compileConstants.debug && typeof command != 'function') {
            throw new Error(`Command name=${debugQuoteString(name)} is not a function: ${command}.`)
        }
        if (this.namedCommands[name]) {
            if (compileConstants.debug)
                throw new Error(`A named command is already registered under the name: ${name}. The conflict occurred in: ${this.moduleName}.`);
            else
                throw new Error('command already exists');
        }

        this.namedCommands[name] = (...innerArgs) => mapCommandResult(command.apply(this, innerArgs.map(a => unmapKnockoutObservables(a, true))))
    }

    public unregisterNamedCommand = (name: string) => {
        if (name in this.namedCommands) {
            delete this.namedCommands[name];
        }
    }
}
