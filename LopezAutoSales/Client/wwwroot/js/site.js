window.blazorAlert = (message) => {
    alert(message);
};

window.readFiles = (id) => {
    var input = document.getElementById(id);
    function getBase64(file) {
        const reader = new FileReader();
        return new Promise(resolve => {
            reader.onload = ev => {
                resolve(ev.target.result);
            };
            reader.readAsDataURL(file);
        })
    }
    // here will be array of promisified functions
    const promises = [];
    // loop through fileList with for loop
    for (let i = 0; i < input.files.length; i++) {
        promises.push(getBase64(input.files[i]));
    }
    // array with base64 strings
    return Promise.all(promises);
};