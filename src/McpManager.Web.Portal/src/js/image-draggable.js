import $ from 'jquery';
$(document).ready(function () {
    $(document).on("click", ".image-input-dropzone, .image-input-dropzone input", function (e) {
        e.stopPropagation();
        $(this).find("input").click();
    });

    $(document).on("change", ".image-input-dropzone input", function () {
        const [file] = this.files;
        let $wrapper = $(this).closest(".image-input-dropzone");

        if (file) {
            $wrapper.find(".drop-message").addClass("hidden");
            $wrapper.find("img").attr("src", URL.createObjectURL(file)).closest(".image-view").removeClass("hidden");
            $wrapper.find(".delete-input").val("false");
        }
    });

    // On delete the image
    $(document).on("click", ".image-input-dropzone .actions .delete", function (e) {
        e.preventDefault();
        e.stopPropagation();
        const $wrapper = $(this).closest(".image-input-dropzone");
        $wrapper.find("input").val(""); // clear the input value
        $wrapper.find(".drop-message").removeClass("hidden");
        $wrapper.find(".image-view").addClass("hidden");
        $wrapper.find(".delete-input").val("true");

    });


    $(".image-input-dropzone").each(function () {
        // where files are dropped + file selector is opened
        let $this = $(this);

        // where images are previewed
        let $imageView = $this.find(".image-view");

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
            if (e.originalEvent.dataTransfer.effectAllowed == "copyMove")
                return;
            let dt = e.originalEvent.dataTransfer, files = dt.files;

            if (files.length) {
                handleFiles(files);
            }
        }

        $this.on('drop', handleDrop);

        function handleFiles(files) {
            $this.find("input").get(0).files = files;
            previewAndUploadImage(files[0]);
        }

        function previewAndUploadImage(image) {
            $imageView.removeClass("hidden");
            $this.find(".drop-message").addClass("hidden");
            let img = $imageView.find("img").get(0);
            let reader = new FileReader();
            reader.onload = function (e) {
                img.src = e.target.result;
            }
            reader.readAsDataURL(image);
        }
    });
});
