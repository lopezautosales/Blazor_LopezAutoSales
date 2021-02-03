window.toggleModal = (id) => {
	var myModalEl = document.getElementById(id);
	bootstrap.Modal.getInstance(myModalEl).toggle();
};
