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