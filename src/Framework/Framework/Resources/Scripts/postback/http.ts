import { getVirtualDirectory, getViewModel, getState, getStateManager, options } from '../dotvvm-base';
import { DotvvmPostbackError } from '../shared-classes';
import { logInfoVerbose, logWarning } from '../utils/logging';
import { keys } from '../utils/objects';
import { addLeadingSlash, concatUrl } from '../utils/uri';

export type WrappedResponse<T> = {
    readonly result: T,
    readonly response?: Response
}

export async function getJSON<T>(url: string, spaPlaceHolderUniqueId?: string, signal?: AbortSignal, additionalHeaders?: { [key: string]: string }): Promise<WrappedResponse<T>> {
    const headers = new Headers();
    headers.append('Accept', 'application/json');
    if (compileConstants.isSpa && spaPlaceHolderUniqueId) {
        headers.append('X-DotVVM-SpaContentPlaceHolder', spaPlaceHolderUniqueId);
    }
    appendAdditionalHeaders(headers, additionalHeaders);

    return await fetchJson<T>(url, { headers: headers, signal });
}

export async function postJSON<T>(url: string, postData: any, signal: AbortSignal | undefined, additionalHeaders?: { [key: string]: string }): Promise<WrappedResponse<T>> {
    const headers = new Headers();
    headers.append('Content-Type', 'application/json');
    headers.append('X-DotVVM-PostBack', 'true');
    appendAdditionalHeaders(headers, additionalHeaders);
    if (postData.length > 1000 && options.compressPOST) {
        postData = await compressString(postData, headers)
    }

    return await fetchJson<T>(url, { body: postData, headers: headers, method: "POST", signal });
}

export async function fetchJson<T>(url: string, init: RequestInit): Promise<WrappedResponse<T>> {
    let response: Response;
    try {
        response = await fetch(url, init);
    }
    catch (err) {
        throw new DotvvmPostbackError({ type: "network", err });
    }

    const errorResponse = response.status >= 400;
    const isJson = response.headers.get("content-type") && response.headers.get('content-type')!.match(/^application\/json/);

    if (errorResponse || !isJson) {
        throw new DotvvmPostbackError({ type: "serverError", status: response.status, responseObject: (isJson ? await response.json() : null), response });
    }

    return { result: await response.json(), response };
}

export async function fetchCsrfToken(signal: AbortSignal | undefined): Promise<string> {
    let token = getState().$csrfToken
    if (token == null) {
        let response;
        try {
            const url = addLeadingSlash(concatUrl(getVirtualDirectory() || "", "___dotvvm-create-csrf-token___"));
            response = await fetch(url, { signal });
        }
        catch (err) {
            logWarning("postback", `CSRF token fetch failed.`);
            throw new DotvvmPostbackError({ type: "network", err });
        }

        if (response.status != 200) {
            logWarning("postback", `CSRF token fetch failed. HTTP status: ${response.statusText}`);
            throw new DotvvmPostbackError({ type: "csrfToken" });
        }

        token = await response.text()
        getStateManager().setState({ ...getState(), $csrfToken: token })
    }
    return token
}

export async function retryOnInvalidCsrfToken<TResult>(postbackFunction: () => Promise<TResult>, iteration: number = 0, customErrorHandler: () => void = () => {}): Promise<TResult> {
    try {
        const result = await postbackFunction();
        return result;
    }
    catch (err) {
        // if the CSRF token is invalid, retry the postback
        if (err instanceof DotvvmPostbackError) {
            if (err.reason.type === "serverError") {
                if (err.reason.responseObject?.action === "invalidCsrfToken") {
                    logInfoVerbose("postback", "Resending postback due to invalid CSRF token.");
                    getStateManager().updateState(u => ({ ...u, $csrfToken: undefined }))

                    if (iteration < 3) {
                        return await retryOnInvalidCsrfToken(postbackFunction, iteration + 1);
                    }
                }
            }
        }
        customErrorHandler();
        throw err;
    }
}

function appendAdditionalHeaders(headers: Headers, additionalHeaders?: { [key: string]: string }) {
    if (additionalHeaders) {
        for (const key of keys(additionalHeaders)) {
            headers.append(key, additionalHeaders[key]);
        }
    }
}

function compressString(data: string, headers: Headers) {
    if (!window.CompressionStream) {
        return data
    }
    headers.append('Content-Encoding', 'gzip')
    const blob = new Blob([data], { type: 'text/plain' })
    const stream = blob.stream().pipeThrough(new CompressionStream('gzip'))
    return new Response(stream).blob()
}
