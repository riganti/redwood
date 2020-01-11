import { initCore, getViewModel, getViewModelObservable, initBindings } from "./dotvvm-base"
import addPolyfills from './DotVVM.Polyfills'
import * as events from './events'
import * as spa from "./spa/spa"
import * as validation from './validation/validation'
import { postBack } from './postback/postback'
import { serialize } from './serialization/serialize'
import { deserialize } from './serialization/deserialize'
import registerBindingHandlers from './binding-handlers/register'
import * as evaluator from './utils/evaluator'
import * as globalize from './DotVVM.Globalize'
import { staticCommandPostback } from './postback/staticCommand'
import { applyPostbackHandlers } from './postback/postback'

if (compileConstants.nomodules) {
    addPolyfills()
}

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
}

const dotvvmExports = {
    evaluator: {
        getDataSourceItems: evaluator.getDataSourceItems,
        wrapObservable: evaluator.wrapObservable
    },
    // fileUpload,
    // getXHR,
    globalize: {
        formatString: globalize.formatString
    },
    // postBackHandlers,
    // handleSpaNavigation,
    // buildUrlSuffix,
    // isSpaReady,
    // buildRouteUrl,
    staticCommandPostback,
    applyPostbackHandlers,
    validation: validation.globalValidationObject,
    postBack,
    init,
    events,
    viewModels: {
        root: {
            get viewModel() { return getViewModel() }
        }
    },
    viewModelObservables: {
        get root() { return getViewModelObservable(); }
    },
    serialization: {
        serialize,
        deserialize
    }
}

declare global {
    const dotvvm: typeof dotvvmExports;

    interface Window {
        dotvvm: typeof dotvvmExports
    }
}

window.dotvvm = dotvvmExports;
