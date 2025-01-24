import { wrapObservable } from '../utils/knockout';
import { updateTypeInfo } from '../metadata/typeMap';

export function showUploadDialog(sender: HTMLElement) {
    // trigger the file upload dialog
    let fileUpload = <HTMLInputElement>sender.parentElement!.parentElement!.querySelector("input[type=file]");
    fileUpload!.click();
}

export function reportProgress(inputControl: HTMLInputElement, isBusy: boolean, progress: number, result: DotvvmStaticCommandResponse<DotvvmFileUploadData[]> | string): void {
    // find target control viewmodel
    const targetControl = <HTMLDivElement> inputControl.parentElement!;
    const viewModel = <DotvvmFileUploadCollection> ko.dataFor(targetControl.firstChild!);

    // determine the status
    if (typeof result === "string") {
        // error during upload
        viewModel.Error(result);
    } else if ("result" in result) {
        // files were uploaded successfully
        viewModel.Error("");
        updateTypeInfo(result.typeMetadata);

        // if multiple files are allowed, we append to the collection
        // if it's not, we replace the collection with the one new file
        const allowMultiple = inputControl.multiple
        const filesObservable = viewModel.Files as DotvvmObservable<any>;
        const newFiles = allowMultiple ? [...filesObservable.state, ...result.result] : result.result;
        filesObservable.setState!(newFiles)

        // call the handler
        if (((<any> targetControl.attributes)["data-dotvvm-upload-completed"] || { value: null }).value) {
            new Function((<any> targetControl.attributes)["data-dotvvm-upload-completed"].value).call(targetControl);
        }
    }
    viewModel.Progress(progress);
    viewModel.IsBusy(isBusy);
}

