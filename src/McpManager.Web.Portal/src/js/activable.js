import $ from 'jquery';
import {showMessage} from './messages';

//#Active buttons
$(document).ready(function () {
    CreateActiveAction();
});

function CreateActiveAction(element) {
    var elements = [];
    if (element === undefined || element === null || element.length <= 0) {
        $("body").find("input[data-model].activable").each(function() {
            elements.push($(this));
        });
    } else {
        elements.push(element);
    }
    for (let i = 0; i < elements.length; i++) {
        let $input = elements[i];
        $input.on("change", function () {
            let $this = $(this);
            let modelName = $this.attr("data-model");
            let key = $this.attr("data-key");
            if (modelName === undefined || key === undefined) return;

            $.post("/api/Activable/Index", { modelName, key })
            .done(function (data) {
                if (data === true) {
                    showMessage("Record activated successfully.", 1);
                    $this.prop("checked", true);
                } else if (data === false) {
                    showMessage("Record deactivated successfully.", 1);
                    $this.prop("checked", false);
                } else {
                    location.reload();
                }
            }).fail(function (xhr) {
                $this.prop("checked", !$this.prop("checked"));
                if (xhr.status === 404 || xhr.status === 400)
                    showMessage(xhr.responseText, -1);
            });
        });
    }
}

//#Active buttons
