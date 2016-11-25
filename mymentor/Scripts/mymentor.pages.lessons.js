(function () {
    lessons.prototype = new _mymentor.Page;
    lessons.prototype.base = _mymentor.Page.prototype;

    function lessons(name, settings) {
        this.name = name;
        this.settings = settings;

        // add modules here...
        this.addModule("toraCategorySearch");
        this.addModule("updateLesson");
        this.addModule("updateBundle");
        this.addModule("createCupon");
        this.addModule("querystring");
        this.addModule("shoppingCart");
    };

    lessons.prototype.init = function () {
        this.base.init.call(this);

        $($('.teacherSound')).load(function (event) {
            event.stopImmediatePropagation();
        });

    };

    lessons.prototype.onLoad = function () {
        _mymentor.modules.toraCategorySearch.bindGuiEvents();
        this.base.onLoad.call(this);
    }

    _mymentor.addPage("lessons", lessons);
})();

var _activeWait;
var _activeStop;

function beginDownloadClip(sender, e) {
    $("#clipPlayer").hide();
    $(".mentorDownloading").hide();
    $(".teacherSound").show();
    $('*[data-action="stopClip"]').hide();

    $(e.context).hide();
    _activeWait = $(e.context).siblings(".mentorDownloading");
    _activeStop = $(e.context).siblings('*[data-action="stopClip"]');
    _activeWait.show();
    
    _activeStop.click(function () {
        document.getElementById("player").pause();
        _activeWait.siblings(".teacherSound").show();
        _activeStop.hide();
    });
}

function playClip(sender, e) {
    var player = document.getElementById("player");
  //  player.pause();
  //  player.currentTime = 0;
    player.play();
    _activeWait.hide();
    _activeStop.show();
}

function showClipDetailsDialog() {

    $("#clipDetailsContainer").dialog({
        modal: true,
        width:800
    });
}

function playerEnded() {
    _activeWait.hide();
    _activeWait.siblings(".teacherSound").show();
    $('*[data-action="stopClip"]').hide();
}