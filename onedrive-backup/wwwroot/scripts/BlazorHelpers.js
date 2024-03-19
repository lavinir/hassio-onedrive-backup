var blazorPassThrough = false;
var settingsPopover;
var saveToast;

function addTooltips() {
    const tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"]')
    const tooltipList = [...tooltipTriggerList].map(tooltipTriggerEl => new bootstrap.Tooltip(tooltipTriggerEl))
}

function refreshTooltipContent(...elements) {
    elements.forEach(elementId => {
        const element = document.getElementById(elementId);
        const tooltip = bootstrap.Tooltip.getInstance(element);
        if (tooltip) {
            tooltip.dispose();
            new bootstrap.Tooltip(element);
        }
    });
}

function addToasts() {
    var toastElList = [].slice.call(document.querySelectorAll('.toast'))
    var toastList = toastElList.map(function (toastEl) {
        return new bootstrap.Toast(toastEl)
    })

    saveToast = toastList.filter(t => t._element.id == "saveToast")[0];
}

function showSaveToast() {
    saveToast.show();
}

function showReleaseNotes() {
    var rnModal = new bootstrap.Modal(document.getElementById('rnotesModal'), {
        keyboard: false
    })

    rnModal.show();
}

function copyToClipboard(txt) {
    navigator.clipboard.writeText(txt);
}

function scrollToTop() {
    document.documentElement.scrollTop = 0;
}

function showAlert(title, message, type, linkUrl, linkCaption, message2) {
    const alertPlaceholder = document.getElementById('alertPlaceholder');
    const wrapper = document.createElement('div')
    wrapper.innerHTML = [
        `<div class="alert alert-${type}" role="alert">`,
        `   <div><strong>${title}</strong>${message}<a href="${linkUrl}" class="alert-link">${linkCaption}</a>${message2}</div>`,
        '</div>'
    ].join('')

    alertPlaceholder.append(wrapper)
}

function checkBlazorStatus() {
    var markup = document.documentElement.innerHTML;
    if (markup.includes("<!--HtmlContentFilterTest") == false) {
        blazorPassThrough = true;
        showAlert("Error! ", "Html Content Filtering Detected - Web Interface will not work. If you are using Cloudflare or a similar service please follow", "danger", "https://github.com/lavinir/hassio-onedrive-backup#html-content-filtering-error", " this link ", "for instructions");
    }
}

window.onload = function () {
    checkBlazorStatus();
}