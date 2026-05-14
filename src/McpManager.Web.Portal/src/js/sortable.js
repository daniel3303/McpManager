import $ from 'jquery';

//#Sortable
$(document).ready(function () {
    createSortableTable();
});
function createSortableTable(element) {
    var elements = [];
    if (element === undefined || element === null || element.length <= 0) {
        $("body").find(".table").each(function () {
            if ($(this).find(".sort-handle").length) {
                elements.push($(this));
            }
        });
    } else {
        elements.push(element);
    }
    for (let i = 0; i < elements.length; i++) {
        let $table = elements[i];
        if ($table.find("tbody tr").length > 1) {
            sortable($table.find("tbody"),
                {
                    handle: ".sort-handle",
                    itemSerializer: (serializedItem) => {
                        return $(serializedItem.node).find(".sort-handle").data("key");
                    }
                }
            )[0].addEventListener('sortupdate', function (e) {
                let items = sortable($(e.target), "serialize")[0].items;
                let model = $(e.target).find(".sort-handle").data("model");
                $.ajax({
                    url: "/api/sortable/sort",
                    method: "POST",
                    data: { modelName: model, items: items }
                }).fail(function (jqXHR, textStatus, errorThrown)  {
                    console.log(textStatus);
                });
            });
        } else {
            let $td = undefined;
            if ($table.find(".sort-handle").parentsUntil("td").length === 0) {
                $td = $table.find(".sort-handle").parent("td");
            } else {
                $td = $table.find(".sort-handle").parentsUntil("td").parent();
            }
            let index = $td.index();
            $table.find("td:nth-child(" + (index + 1) + "), th:nth-child(" + (index + 1) + ")").hide();
        }
    }
}
//#Sortable
