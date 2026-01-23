import $ from 'jquery';

/* file input */
$(document).ready(function () {
    initializeFileGroupDraggable();
});

function initializeFileGroupDraggable() {
    const visualExtensions = ["jpg", "png", "gif", "jpeg"];
    function getFileLogoByExtension(extension) {
        extension = extension.replace(".", "");
        let supportedExtensions = ["avi", "csv", "doc", "docx", "file", "jpg", "pdf", "ppt", "pptx", "psd", "txt", "wav", "xls", "xlsx",
            "tiff", "mp3", "bmp", "pst", "vsd", "accdb", "eml", "zip", "mov", "txt", "jsf", "midi", "mpeg", "rar", "psd", "eps", "ai", "ae", "css",
            "html", "avi", "ini", "pub"];
        if (supportedExtensions.includes(extension)) return `/images/file-types/${extension}.png`;
        return "/images/file-types/file.png";
    }

    $(document).find(".files-draggable").each(function () {
        let $this = $(this);
        let $filePreviewRegion = $this.find(".files-preview");
        // delete file
        $this.on("click", ".delete", function (e) {
            e.stopPropagation();
            e.preventDefault();
            let $file = $(e.target).closest(".file-view");
            let fileId = $file.data("file");
            Confirm("Do you want to delete this file?", function () {
                $.ajax({
                    method: "POST",
                    url: "/api/files/filedelete",
                    data: { fileId }
                }).done(function (data) {
                    $file.remove();
                    //hideMessage();
                    showMessage("File deleted successfully.", 1);
                }).fail(function (data) {
                    $file.remove();
                    showMessage("The file could not be deleted.", -1);
                });
            });
        });

        //open file
        $this.on("click", ".file-view", function () {
            let url = $(this).data("filepath");
            window.open(url, "_blank");
        });


        // sortable images
        sortable($this.find(".files-preview"), {
            forcePlaceholderSize: true,
            placeholder: "<div class='file-view'><div class=\"w-100 h-100 bg-grey\"></div></div>",
            itemSerializer: (serializedItem) => {
                return $(serializedItem.node).data("file");
            }
        }
        )[0].addEventListener('sortupdate', function (e) {
            let items = sortable($(e.target), "serialize")[0].items;
            $.ajax({
                url: "/api/sortable/sort",
                method: "POST",
                data: { modelName: "Plataforma.Models.Media.File", items: items }
            }).fail(function (jqXHR, textStatus) {
                alert(jqXHR);
            });
        });

        // create the file selector fake input when clicked on the drop region
        let fakeInput = document.createElement("input");
        fakeInput.type = "file";
        fakeInput.accept = "file/*";
        fakeInput.multiple = true;
        $this.on('click', function () {
            fakeInput.click();
        });

        $(fakeInput).on("change", function () {
            let files = fakeInput.files;
            handleFiles(files);
        });



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
            let dt = e.originalEvent.dataTransfer,
                files = dt.files;

            if (files.length) {
                handleFiles(files);
            }
        }

        $this.on('drop', handleDrop);

        function handleFiles(files) {
            for (let i = 0, len = files.length; i < len; i++) {
                previewAndUploadFile(files[i]);
            }
        }

        function previewAndUploadFile(file) {

            // container
            let fileView = document.createElement("div");
            fileView.className = "file-view";
            $filePreviewRegion.prepend(fileView);

            // previewing image
            let img = document.createElement("img");
            fileView.appendChild(img);
            //hideMessage();

            // progress overlay
            let overlay = document.createElement("div");
            overlay.className = "overlay";
            fileView.appendChild(overlay);

            let extension = file.name.split(".").splice(-1)[0];
            let extensionImage = getFileLogoByExtension(extension);

            let period = file.name.lastIndexOf('.');
            let fileName = file.name.substring(0, period);

            // previewing image
            let name = document.createElement("div");
            fileView.appendChild(name);
            name.className = "filename";

            //hideMessage();

            if (!visualExtensions.includes(extension)) {
                img.src = extensionImage;
            } else {
                let reader = new FileReader();
                reader.onload = function (e) {
                    img.src = e.target.result;
                }
                reader.readAsDataURL(file);
            }

            // create FormData
            let formData = new FormData();
            formData.append('file', file);

            let uploadLocation = '/api/files/FileGroupUpload';
            formData.append('filegroupid', $this.data("repo"));
            formData.append('fileName', fileName);
            formData.append('json', "true");

            let ajax = new XMLHttpRequest();
            ajax.open("POST", uploadLocation, true);

            ajax.onreadystatechange = function (e) {
                if (ajax.readyState === 4) {
                    if (ajax.status === 200) {
                        let fileId = JSON.parse(ajax.response).fileId;
                        let fileNameFromServer = JSON.parse(ajax.response).fileName;
                        let filePath = JSON.parse(ajax.response).filePath;
                        $(fileView).attr("data-file", fileId);
                        $(fileView).attr("data-path", filePath);

                        $(fileView).prepend(`
                            <div class="actions">
                                <button class="btn btn-error btn-xs delete"><i class="icon-trash-can"></i></button>
                            </div>
                        `);
                        name.innerText = fileNameFromServer;
                    } else {
                        $(fileView).remove();
                        showMessage("The file could not be saved. Invalid file.", "Danger");
                    }
                }
            }

            ajax.upload.onprogress = function (e) {
                // change progress
                // (reduce the width of overlay)
                let perc = (e.loaded / e.total * 100) || 100;
                overlay.style.width = 100 - perc;
            }
            ajax.send(formData);
        }

    });
};
