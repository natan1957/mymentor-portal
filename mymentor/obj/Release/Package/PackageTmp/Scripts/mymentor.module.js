(function () {

    function module(name, settings) {

    }

    module.prototype.modules = null;

    module.prototype.addModule = function (name) {
        if (typeof _mymentor.modules[name] === "undefined") {
            alert("module " + name + "does not exist");
        }

        if (this.settings === undefined) {
            this.settings = {};
        }

        if (typeof this.settings[name] === "undefined") {
            this.settings[name] = {};
        }

        this.modules[name] = new _mymentor.modules[name](name, this.settings[name]);
    };

    module.prototype.init = function () {
        for (var name in this.modules) {
            this.modules[name].init.call(this.modules[name]);
        }
    };

    module.prototype.onLoad = function () {
        for (var name in this.modules) {
            this.modules[name].onLoad();
        }
    };

    _mymentor.Module = module;

})();