import $ from 'jquery';

function closeNotificationDropdown() {
    document.activeElement?.blur();
}

// Expose to global scope for onclick handler
window.closeNotificationDropdown = closeNotificationDropdown;

function updateNotificationBadge(count) {
    const badge = document.getElementById('notification-badge');
    if (!badge) return;

    if (count > 0) {
        badge.textContent = count > 99 ? '99+' : count;
        badge.classList.remove('hidden');
    } else {
        badge.classList.add('hidden');
    }
}

function pollNotifications() {
    $.get('/Notifications/UnreadCount')
        .done(function (data) {
            updateNotificationBadge(data.count);
        });
}

function markAllNotificationsAsRead() {
    $.post('/Notifications/ApiMarkAllAsRead')
        .done(function () {
            updateNotificationBadge(0);
            // Update dropdown visuals
            $('#notifications-list a').removeClass('bg-primary/5');
            $('#notifications-list .rounded-full.bg-primary').remove();
            $('#notifications-list .font-medium').removeClass('font-medium').addClass('text-base-content/80');
            $('#mark-all-read-btn').hide();
        });
}

$(document).ready(function () {
    // Only initialize if notification dropdown exists
    if ($('#notification-dropdown').length === 0) return;

    // Poll notifications every 5 seconds
    setInterval(pollNotifications, 5 * 1000);

    // Mark all read button
    $('#mark-all-read-btn').on('click', function (e) {
        e.preventDefault();
        e.stopPropagation();
        markAllNotificationsAsRead();
    });
});
