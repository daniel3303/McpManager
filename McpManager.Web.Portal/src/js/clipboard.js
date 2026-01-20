window.copyToClipboard = function (text) {
    navigator.clipboard.writeText(text);
    window.toastr.success('Copied to clipboard');
};
