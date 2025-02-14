import textbox from './textbox-text'
import textboxFocus from './textbox-select-all-on-focus'
import ssrForeach from './SSR-foreach'
import markupControls from './markup-controls'
import columnVisible from './table-columnvisible'
import enable from './enable'
import checkbox from './checkbox'
import updateProgress from './update-progress'
import gridviewdataset from './gridviewdataset'
import namedCommand from './named-command'
import fileUpload from './file-upload'
import jsComponents from './js-component'
import modalDialog from './modal-dialog'
import appendableDataPager from './appendable-data-pager'
import timer from './timer'

type KnockoutHandlerDictionary = {
    [name: string]: KnockoutBindingHandler
}
const allHandlers: KnockoutHandlerDictionary = {
    ...textbox,
    ...ssrForeach,
    ...markupControls,
    ...textboxFocus,
    ...columnVisible,
    ...enable,
    ...checkbox,
    ...updateProgress,
    ...gridviewdataset,
    ...namedCommand,
    ...fileUpload,
    ...jsComponents,
    ...modalDialog,
    ...appendableDataPager,
    ...timer
}

export default allHandlers
