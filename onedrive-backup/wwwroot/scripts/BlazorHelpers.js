function addTooltips() {
    $('[data-bs-toggle="tooltip"]').tooltip({
        trigger: 'hover'
    });
    $('[data-bs-toggle="tooltip"]').on('mouseleave', function () {
        $(this).tooltip('hide');
    });
    $('[data-bs-toggle="tooltip"]').on('click', function () {
        $(this).tooltip('dispose');
    });
}

function copyToClipboard(txt) {
    navigator.clipboard.writeText(txt);
}

function showAlert(message, type) {
    const alertPlaceholder = document.getElementById('alertPlaceholder');
    const wrapper = document.createElement('div')
    wrapper.innerHTML = [
        `<div class="alert alert-${type} alert-dismissible" role="alert">`,
        `   <div>${message}</div>`,
        '   <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>',
        '</div>'
    ].join('')

    alertPlaceholder.append(wrapper)
}