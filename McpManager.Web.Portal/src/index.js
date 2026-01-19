import jQuery from 'jquery';
window.$ = window.jQuery = jQuery;

// jQuery and validation
import 'jquery-validation';
import 'jquery-validation-unobtrusive';

// Main styles (Tailwind v4 + DaisyUI v5)
import './css/main.css';

// Custom modules
import './js/activable';
import './js/clipboard';
import './js/confirm-modal';
import './js/datepicker';
import './js/file-draggable';
import './js/file-inputs';
import './js/filegroup-draggable';
import './js/image-draggable';
import './js/image-inputs';
import './js/input-mask';
import './js/markdown-editor';
import './js/messages';
import './js/modals';
import './js/mvc-grid';
import './js/notifications';
import './js/numeric-inputs';
import './js/selects';
import './js/sortable';

// Provide toastr to window
import toastr from 'toastr';
window.toastr = toastr;

// Provide ApexCharts to window
import ApexCharts from 'apexcharts';
window.ApexCharts = ApexCharts;