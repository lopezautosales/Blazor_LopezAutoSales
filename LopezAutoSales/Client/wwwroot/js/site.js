window.toggleModal = (id) => {
	var myModalEl = document.getElementById(id);
	bootstrap.Modal.getInstance(myModalEl).toggle();
};

window.scrollToElement = (id) => {
	document.getElementById(id)?.scrollIntoView({ behavior: "smooth", block: "start" });
};
