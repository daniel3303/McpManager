import InputMask from "inputmask";
import $ from 'jquery';


$(document).ready(function(){
    InputMask().mask(document.querySelectorAll("input"));
});
