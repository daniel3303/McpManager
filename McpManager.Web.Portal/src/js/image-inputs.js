import $ from 'jquery';

//#Image inputs
$(document).ready(function () {
    $(".image-input-delete").on("click", function () {
        $(this).siblings("input").val(true);
        $(this).siblings("img").remove();
        $(this).parent().siblings(".custom-file").removeClass("d-none");
        $(this).remove();
    });
});

//#Image inputs
