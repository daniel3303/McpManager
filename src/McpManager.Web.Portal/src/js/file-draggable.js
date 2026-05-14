import $ from 'jquery';

/* file input */
$(document).ready(function () {
    const visualExtensions = ["jpg", "png", "gif", "jpeg"];
    function getFileLogoByExtension(extension){
        //extension = extension.replace(".", "");
        let supportedExtensions = ["avi", "csv", "doc", "docx", "file", "jpg", "pdf", "ppt", "pptx", "psd", "txt", "wav", "xls", "xlsx",
            "tiff", "mp3", "bmp", "pst", "vsd", "accdb", "eml", "zip", "mov", "txt", "jsf", "midi", "mpeg", "rar", "psd", "eps", "ai", "ae", "css",
            "html", "avi", "ini", "pub"];
        if (supportedExtensions.includes(extension)) return `/images/file-types/${extension}.png`;
        return "/images/file-types/file.png";
    }


    $(document).on("click", ".file-input-dropzone, .file-input-dropzone input", function (e) {
        e.stopPropagation();
        $(this).find("input").click();
    });

    $(document).on("change", ".file-input-dropzone input", function () {
        const [file] = this.files;
        let $wrapper = $(this).closest(".file-input-dropzone");
        let $fileView = $wrapper.find(".file-view");

        if (file) {
            let img = $fileView.find("img").get(0);
            let extension = file.name.split(".").splice(-1)[0];
            let extensionImage = getFileLogoByExtension(extension);
            $fileView.removeClass("hidden");

            if(!visualExtensions.includes(extension)){
                img.src = extensionImage;
            }else{
                let reader = new FileReader();
                reader.onload = function (e) {
                    img.src = e.target.result;
                }
                reader.readAsDataURL(file);
            }
            $wrapper.find(".drop-message").addClass("hidden");
            $wrapper.find(".delete-input").val("false");
        }
    });

    // On delete the file
    $(document).on("click", ".file-input-dropzone .actions .delete", function (e) {
        e.preventDefault();
        e.stopPropagation();
        const $wrapper = $(this).closest(".file-input-dropzone");
        $wrapper.find("input").val(""); // clear the input value
        $wrapper.find(".drop-message").removeClass("hidden");
        $wrapper.find(".file-view").addClass("hidden");
        $wrapper.find(".delete-input").val("true");
    });


    $(".file-input-dropzone").each(function () {
        // where files are dropped + file selector is opened
        let $this = $(this);

        // where files are previewed
        let $fileView = $this.find(".file-view");

        // drag events
        $this.on('dragenter', function (e) {
            $this.addClass("dragover");
            e.preventDefault();
            e.stopPropagation();
            return false;
        });
        $this.on('dragover', function (e) {
            e.preventDefault();
            e.stopPropagation();
            return false;
        });
        $this.on('dragleave', function (e) {
            $this.removeClass("dragover");
            e.preventDefault();
            e.stopPropagation();
            return false;
        });

        function handleDrop(e) {
            e.preventDefault();
            e.stopPropagation();
            let dt = e.originalEvent.dataTransfer, files = dt.files;

            if (files.length) {
                handleFiles(files);
            }
        }

        $this.on('drop', handleDrop);

        function handleFiles(files) {
            $this.find("input").get(0).files = files;
            previewFile(files[0]);
        }

        function previewFile(file) {
            let extension = file.name.split(".").splice(-1)[0];
            let extensionImage = getFileLogoByExtension(extension);
            $fileView.removeClass("hidden");
            $this.find(".drop-message").addClass("hidden");

            let img = $fileView.find("img").get(0);
            if(!visualExtensions.includes(extension)){
                img.src = extensionImage;
            }else{
                let reader = new FileReader();
                reader.onload = function (e) {
                    img.src = e.target.result;
                }
                reader.readAsDataURL(file);
            }
        }
    });
});
