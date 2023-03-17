var blazorPassThrough = false;

function addTooltips() {
    const tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"]')
    const tooltipList = [...tooltipTriggerList].map(tooltipTriggerEl => new bootstrap.Tooltip(tooltipTriggerEl))
}

function copyToClipboard(txt) {
    navigator.clipboard.writeText(txt);
}

function showAlert(message, type) {
    const alertPlaceholder = document.getElementById('alertPlaceholder');
    const wrapper = document.createElement('div')
    wrapper.innerHTML = [
        `<div class="alert alert-${type}" role="alert">`,
        `   <div>${message}</div>`,
        '</div>'
    ].join('')

    alertPlaceholder.append(wrapper)
    alert("duda");
}

function checkBlazorStatus() {
    alert(blazorPassThrough);
    var markup = document.documentElement.innerHTML;
    // alert(markup);
    if (markup.includes("<!--Blazor") == true) {
//        showAlert("Blazor is not running. Please refresh the page.", "danger");
        //alert("gotit");
        blazorPassThrough = true;
    }
//    else {
//        alert("oger");
//    }
}

function alertIfBlazorDisabled() {
    if (blazorPassThrough == true) {
        showAlert("Duda is an Oger", "danger");
        // alert("hmmm");
    }
}