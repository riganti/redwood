using System;

namespace DotVVM.Framework.Hosting
{
    public class HostingConstants
    {
        public const string DotvvmRequestContextKey = "dotvvm.requestContext";
        public const string DotvvmIsErrorHandlingKey = "dotvvm.isErrorHandling";

        public const string GlobalizeCultureUrlIdParameter = "id";
        public const string ResourceRouteName = "_dotvvm/resource";
        public const string FileUploadHandlerMatchUrl = "_dotvvm/fileUpload";
        public const string CsrfTokenMatchUrl = "___dotvvm-create-csrf-token___";

        public const string SpaContentPlaceHolderHeaderName = "X-DotVVM-SpaContentPlaceHolder";
        public const string PostBackHeaderName = "X-DotVVM-PostBack";
        [Obsolete("Use PostBackHeaderName instead")]
        public const string SpaPostBackHeaderName = PostBackHeaderName;
        public const string SpaContentPlaceHolderID = "__dot_SpaContentPlaceHolder";
        public const string SpaUrlIdentifier = "___dotvvm-spa___";

        public const string SpaContentPlaceHolderDataAttributeName = "data-dotvvm-spacontentplaceholder";

        public const string DotvvmFileUploadAsyncHeaderName = "X-DotVVM-AsyncUpload";

        public const string HostAppModeKey = "host.AppMode";

        /// <summary>
        /// When this key is set to true in the OWIN environment, the request culture will not be set by DotVVM to config.DefaultCulture.
        /// Use this key when the request culture is set by the host or the middleware preceding DotVVM.
        /// See https://github.com/riganti/dotvvm/blob/93107dd07127ff2bd29c2934f3aa2a26ec2ca79c/src/Samples/Owin/Startup.cs#L34
        /// </summary>
        public const string OwinDoNotSetRequestCulture = "OwinDoNotSetRequestCulture";
    }
}
