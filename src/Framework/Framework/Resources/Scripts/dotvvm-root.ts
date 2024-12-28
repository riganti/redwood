import { initCore, getViewModel, getViewModelObservable, initBindings, getCulture, getState, getStateManager, options } from "./dotvvm-base"
import * as events from './events'
import * as spa from "./spa/spa"
import * as validation from './validation/validation'
import { postBack } from './postback/postback'
import { serialize } from './serialization/serialize'
import { serializeDate, parseDate, parseDateOnly, parseTimeOnly } from './serialization/date'
import { deserialize } from './serialization/deserialize'
import registerBindingHandlers from './binding-handlers/register'
import * as evaluator from './utils/evaluator'
import * as globalize from './DotVVM.Globalize'
import { staticCommandPostback } from './postback/staticCommand'
import { applyPostbackHandlers } from './postback/postback'
import { isSpaReady } from "./spa/spa"
import { buildRouteUrl, buildUrlSuffix } from './controls/routeLink'
import * as fileUpload from './controls/fileUpload'
import { handleSpaNavigation } from './spa/spa'
import { postbackHandlers } from './postback/handlers'
import * as spaEvents from './spa/events'
import { isPostbackRunning } from "./postback/internal-handlers"
import * as api from './api/api'
import * as eventHub from './api/eventHub'
import * as viewModuleManager from './viewModules/viewModuleManager'
import { notifyModuleLoaded } from './postback/resourceLoader'
import { logError, logWarning, logInfo, logInfoVerbose, level, logPostBackScriptError, setLogger } from "./utils/logging"
import * as metadataHelper from './metadata/metadataHelper'
import { StateManager } from "./state-manager"
import { DotvvmEvent } from "./events"
import translations from './translations/translations'
import { loadDataSet, postProcessors as loaderPostProcessors } from './dataset/loader'
import * as dataSetTranslations from './dataset/translations'

if (window["dotvvm"]) {
    throw new Error('DotVVM is already loaded!')
}
function init(culture: string) {
    initCore(culture)
    registerBindingHandlers()
    validation.init()

    if (compileConstants.isSpa) {
        spa.init()
    }

    initBindings()

    events.initCompleted.trigger({})
}

const dotvvmExports = {
    getCulture: getCulture,
    evaluator: {
        getDataSourceItems: evaluator.getDataSourceItems,
        wrapObservable: evaluator.wrapObservable
    },
    fileUpload: {
        reportProgress: fileUpload.reportProgress,
        showUploadDialog: fileUpload.showUploadDialog
    },
    api: {
        isLoading: api.isLoading,
        invoke: api.invoke,
        refreshOn: api.refreshOn
    },
    eventHub: {
        get: eventHub.get,
        notify: eventHub.notify
    },
    globalize,
    postBackHandlers: postbackHandlers,
    postbackHandlers: postbackHandlers,
    buildUrlSuffix,
    buildRouteUrl,
    staticCommandPostback,
    applyPostbackHandlers,
    validation: validation.globalValidationObject,
    postBack,
    init,
    registerGlobalComponent: viewModuleManager.registerGlobalComponent,
    isPostbackRunning,
    events: (compileConstants.isSpa ?
             { ...events, ...spaEvents } :
             events) as (Partial<typeof spaEvents> & typeof events),
    viewModels: {
        root: {
            get viewModel() { return getViewModel() }
        }
    },
    get state() { return getState() },
    patchState(a: any) {
        getStateManager().patchState(a)
    },
    setState(a: any) { getStateManager().setState(a) },
    updateState(updateFunction: StateUpdate<any>) { getStateManager().updateState(updateFunction) },
    get rootStateManager() { return getStateManager() },
    viewModelObservables: {
        get root() { return getViewModelObservable(); }
    },
    serialization: {
        serialize,
        serializeDate,
        parseDate,
        parseDateOnly,
        parseTimeOnly,
        deserialize
    },
    metadata: {
        getTypeId: metadataHelper.getTypeId,
        getTypeMetadata: metadataHelper.getTypeMetadata,
        getEnumMetadata: metadataHelper.getEnumMetadata
    },
    viewModules: {
        registerOne: viewModuleManager.registerViewModule,
        init: viewModuleManager.initViewModule,
        call: viewModuleManager.callViewModuleCommand,
        registerMany: viewModuleManager.registerViewModules
    },
    resourceLoader: {
        notifyModuleLoaded
    },
    log: {
        logError,
        logWarning,
        logInfo,
        logInfoVerbose,
        logPostBackScriptError,
        level,
        setLogger
    },
    options,
    translations: translations as any,
    dataSet: {
        loadDataSet: loadDataSet,
        postProcessors: loaderPostProcessors,
        translations: dataSetTranslations.translations
    },
    StateManager,
    DotvvmEvent,
}

if (compileConstants.isSpa) {
    (dotvvmExports as any).isSpaReady = isSpaReady;
    (dotvvmExports as any).handleSpaNavigation = handleSpaNavigation;
}

if (compileConstants.debug) {
    (dotvvmExports as any).debug = true
}
if (false) {
    // just check that the types are assignable
    const test: DotvvmStateContainer<any> = dotvvm
}
declare global {
    interface DotvvmGlobalExtensions {}
    type DotvvmGlobal = DotvvmGlobalExtensions & typeof dotvvmExports & DotvvmStateContainer<any> & { debug?: true, isSpaReady?: typeof isSpaReady, handleSpaNavigation?: typeof handleSpaNavigation }

    const dotvvm: DotvvmGlobal;

    interface Window {
        dotvvm: DotvvmGlobal
    }
}

window.dotvvm = dotvvmExports;

export default dotvvmExports
