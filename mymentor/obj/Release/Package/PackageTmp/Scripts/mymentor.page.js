(function () {

    function Page(name, settings) {
        if (arguments.length === 0) return;

        this.name = name;
        this.settings = settings;
    };

    Page.prototype.modules = {};

    Page.prototype.addModule = function (name) {
        if (typeof _mymentor.modules[name] === "undefined") {
            alert("module " + name + " does not exist");
        }

        if (typeof this.settings === "undefined") {
            this.settings[name] = {};
        }

        this.modules[name] = new _mymentor.modules[name](name, this.settings[name]);
    };

    Page.prototype.init = function () {
        for (var name in this.modules) {
            this.modules[name].init();
        }
    };

    Page.prototype.onLoad = function () {

        //$.validator.setDefaults({
        //    ignore:"",
        //    // any other default options and/or rules
        //});

        for (var name in this.modules) {
            this.modules[name].onLoad();
        }


       

    };

    _mymentor.Page = Page;
    _mymentor.addPage("default", Page);
})();