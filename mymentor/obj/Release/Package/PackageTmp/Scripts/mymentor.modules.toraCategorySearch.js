

(function () {
    toraCategorySearch.prototype = new _mymentor.Module;
    toraCategorySearch.prototype.base = _mymentor.Module.prototype;

    var _vm;

    var m = {
        setupTreeView: function () {
            $.ui.dynatree.nodedatadefaults["icon"] = false;
            $("#residenceView").dynatree({
                checkbox: false,
                fx: { height: "toggle", duration: 200 },
                onActivate: function (node) {
                },
                children: JSON.parse($("#ResidenceJson").val()),
                onPostInit: function () {
                    // Set RTL attribute on init
                    // this.$tree.find("ul.dynatree-container").attr("DIR", "RTL").addClass("dynatree-rtl");
                },
                selectMode: 2,
                onClick: function (node, event) {
                    if (event.target.className != "dynatree-expander") {
                        if (!node.data.isFolder) {
                            var isCity = node.getLevel() == 4;
                            $("#residenceView").slideUp(200);
                            if (isCity) {
                                _vm.selectedCityName(node.data.title);
                                if (node.data.key.length > 0) {
                                    _vm.selectedCityId(node.data.key);
                                } else {
                                    _vm.selectedCityId(node.data.title);
                                }
                                _vm.selectedResidenceName("");
                                _vm.selectedResidenceId("");
                            } else {
                                _vm.selectedResidenceName(node.data.title);
                                _vm.selectedResidenceId(node.data.key);

                                _vm.selectedCityName("");
                                _vm.selectedCityId("");
                            }
                            
                        }
                    }
                }
            });

            $("#residenceView").prepend("<div class=\"tree-clear-selection\" id=\"cleartree\">" + $("#teachingArea").data("clear-selection") + "</div>");
            $(".tree-clear-selection").bind("click", function () {
                _vm.selectedResidenceId("");
                _vm.selectedCityName("");
                _vm.selectedCityId("");
                $("#residenceView").slideUp(400);
                _vm.selectedResidenceName($("#teachingArea").data("placeholder"));
            });
        },

        setupDatepicker: function () {
            var datePicker = $("#datepicker");
            datePicker.datepicker({
                showOn: "button",
                buttonImage: "/content/images/calendar.gif",
                buttonImageOnly: true,
                showOn: 'both'                
            });
            datePicker.datepicker("option", $.datepicker.regional[datePicker.data("regional")]);
            datePicker.datepicker('option', 'dateFormat', 'dd/mm/yy');
            datePicker.datepicker("option", "isRTL", datePicker.data("isrtl") == "True");
        },

        setupCategort1And2Hierarchy: function () {
            var category1Change = function () {
                var selectedValue = $("*[name='category1']").val();
                var category2 = $("#category2");
                var defaultOption = category2.children().first();

                if (selectedValue.length > 0) {
                    
                    category2.children().remove();
                    category2.append(defaultOption);
                    var cat2DataItems = category2.data("items");
                    $.each(cat2DataItems, function(index, value) {
                        if (value.ParentCategoryId === selectedValue) {
                            category2.append("<option value=\"" + value.Key + "\">" + value.Value + "</option>");
                        }
                    });

                    //if ($("#category2_hidden").val().length > 0) {
                    //    category2.val($("#category2_hidden").val());
                    //}
                } else {
                    category2.children().remove();
                    category2.append(defaultOption);
                }
            }

            category1Change();
            var cat1Filter = $("*[name='category1']")[0];

            $(cat1Filter).change(function () {
                category1Change();
            });
        },

        setupResidencePostbackData: function () {
            var selectedResidenceId = $("#residenceId").data("selected-residence-id");
            var selectedResidenceName = $("#category6").data("selected-residence-name");
            var selectedCityName = $("#category8").data("selected-residence-name");
            var selectedCityId = $("#cityId").data("selected-residence-id");

            $("#category6").val(selectedResidenceName);
            $("#category8").val(selectedCityName.length>0 ? selectedCityName:selectedCityId);

            if(selectedResidenceName.length > 0){
                _vm.selectedResidenceName(selectedResidenceName);
                _vm.selectedResidenceId(selectedResidenceId);
            }
            else if (selectedCityName.length > 0 || selectedCityId.length > 0) {
                _vm.selectedCityName(selectedCityName.length > 0 ? selectedCityName : selectedCityId);
                _vm.selectedCityId(selectedCityId);
            }
            else {
                _vm.selectedResidenceName($("#teachingArea").data("placeholder"));
            }
        },
        setupSortPoskbackData: function () {
            var selectedDate = $("#selectedSort").data("selected-sort");
            $("#selectedSort").val(selectedDate);
        }
    }

    function toraCategorySearch(name, settings) {
        this.name = name;
    };

    toraCategorySearch.prototype.init = function () {
        this.base.init.call(this);
    };

    toraCategorySearch.prototype.onLoad = function () {

        this.base.onLoad.call(this);
     
        _vm = new viewModel();
        m.setupTreeView();
        m.setupDatepicker();
        m.setupCategort1And2Hierarchy();
        m.setupSortPoskbackData();
        m.setupResidencePostbackData();
       
        ko.applyBindings(_vm, $(".lessonSelects")[0]);
    };

    function viewModel() {
        var self = this;

        this.category1SelectedValue = ko.observable();

        this.category2SelectedValue = ko.observable($("#category2_hidden").val());

        this.category3SelectedValue = ko.observable();

        this.category4SelectedValue = ko.observable();

        this.category5SelectedValue = ko.observable();

        this.selectedResidenceName = ko.observable();        

        this.selectedResidenceId = ko.observable();

        this.selectedCityName = ko.observable();

        this.selectedCityId = ko.observable();

        this.selectedResidence = ko.computed(function () {
            if (self.selectedResidenceName() == undefined || self.selectedResidenceName() == "") {
                return self.selectedCityName();
            }
            return self.selectedResidenceName();
        });

        this.showResidenceView = function (event, sender) {

            if ($("#residenceView").css("display") == "none") {
                var x = sender.pageX;
                var y = sender.pageY;
                $("#residenceView").css("top", 10);
                $("#residenceView").slideDown(400);
                sender.target.contentEditable = true;

                $(document).mouseup(function(e) {
                    var container = $("#residenceView");

                    if (!container.is(e.target) // if the target of the click isn't the container...
                        && container.has(e.target).length === 0) // ... nor a descendant of the container
                    {
                        $("#residenceView").slideUp(400);
                    }
                });
            } else {
                $("#residenceView").slideUp(400);
            }
        };

        this.residenceBlur = function (event, sender) {
            if ($(sender.currentTarget).html() == "") {
                _vm.selectedResidenceId("");
                _vm.selectedResidenceName($("#teachingArea").data("placeholder"));
            }
        };

        this.residencekeyDown = function (event, sender) {
            if (sender.keyCode != 8) //backspace
            {
                return false;
                //alert("keydown");
            }
            return true;
        };

        this.sortChanged = function (event, sender) {
            $("#btnSubmit").click();
        }
    }

    toraCategorySearch.bindGuiEvents = function () {

        $(".blackTooltip").tooltip(tooltipOptions).tooltip("option", "tooltipClass", "blackTooltip");

        $(".whiteTooltip").tooltip(tooltipOptions).tooltip("option", "tooltipClass", "whiteTooltip");

        $(".lessonTop, .lesson_faq").hover(
                function () {
                    var lessonTop = $(this).closest('.lessonWrapper').addClass('hover'); $(lessonTop).find('.lessonTop').addClass('hover');
                }, function () {
                    var lessonTop = $(this).closest('.lessonWrapper').removeClass('hover');

                    if (!$(lessonTop).find('.lessonTop').hasClass('open')) {
                        $(lessonTop).find('.lessonTop').removeClass('hover');
                    }
                }
            );

        // lessons open/close function
        $(".lessonTop .lessonHeaderItem, .lessonTop .lessonDateItem, .lessonTop .lessonTeacherItem td, .lessonTop .lessonPriceItem, .lessonTop .lessonPackageIcon").click(function (e) {
            if (e.target.className === "teacherSound" ||
                e.target.className === "stopClip" ||
                e.target.className === "bordo bold goTobundle") {
                return;                
            }

            var lessonTop = $(this).closest('.lessonTop');

            if (lessonTop.hasClass('hover')) {
                lessonTop.siblings('.teacherDetails:visible, .sendTeacherMessage:visible, .lessonAdmin:visible').slideToggle();
            }

            // start lesson package function
            var isSeconderyLesson = lessonTop.parent().hasClass('seconderyLesson');
            var isMainLesson = lessonTop.parent().hasClass('mainLesson');

            if (isSeconderyLesson || isMainLesson) {
                if (isSeconderyLesson) {

                    var _openedPackages = lessonTop.parent().siblings('.openedLessonPackage').find('.lessonTop');

                    _openedPackages.each(function (index) {
                        $(this).siblings('.teacherDetails:visible, .sendTeacherMessage:visible, .lessonAdmin:visible').slideUp();
                        $(this).siblings('.lessonContent, .lessonAdmin').not(":animated").slideUp();
                    });
                    lessonTop.parent().siblings('.openedLessonPackage').removeClass('openedLessonPackage');

                    lessonTop.parent().toggleClass('openedLessonPackage');
                    var _mainLesson = lessonTop.parent().siblings('.mainLesson').find('.lessonTop');
                    if (lessonTop.parent().siblings('.openedLessonPackage').length > 0 || lessonTop.parent().hasClass('openedLessonPackage')) {
                        _mainLesson.addClass('open');
                    }
                    else { _mainLesson.removeClass('open'); }

                    if (_mainLesson.hasClass('open')) {
                        _mainLesson.addClass('hover');
                    } else {
                        _mainLesson.removeClass('hover');
                    }
                }

                if (isMainLesson) {
                    var seconderyLessons = $(this).closest('.lessonPackage').find('.seconderyLesson, .bundleAdmin');
                    var packageExpandIcon = lessonTop.find('.PackageExpandIcon');

                    seconderyLessons.slideToggle(function () {
                        if ($(this).closest('.lessonPackage').find('.seconderyLesson:visible').length > 0) {
                            packageExpandIcon.text('-');
                        }
                        else {
                            lessonTop.removeClass('hover open');
                            packageExpandIcon.text('+');
                        }


                    });
                }
            }
                // end lesson package function
            else { lessonTop.toggleClass('open'); }


            lessonTop.siblings('.lessonContent, .lessonAdmin').not(":animated").slideToggle();
            if ($(lessonTop).hasClass('open')) {
                $(lessonTop).addClass('hover');
            } else {
                $(lessonTop).removeClass('hover'); }

            e.preventDefault();
        });

        $(".lessonContentInfo .showTeacher").click(function (e) {
            var lessonWrapper = $(this).closest('.lessonWrapper');
            lessonWrapper.find('.teacherDetails').not(":animated").slideToggle();
            lessonWrapper.find('.sendTeacherMessage:visible').slideToggle();

            e.preventDefault();
        });

        $(".lessonContentInfo .sendMessage").click(function (e) {
            var lessonWrapper = $(this).closest('.lessonWrapper');
            lessonWrapper.find('.sendTeacherMessage').not(":animated").slideToggle();
            lessonWrapper.find('.teacherDetails:visible').slideToggle();

            e.preventDefault();
        });

        $(".lessonContent .closeLesson").click(function (e) {
            var lessonWrapper = $(this).closest('.lessonWrapper');
            lessonWrapper.find('.lessonHeaderItem').click();
            e.preventDefault();
        });

        $(".lesson_faq").tooltip("option", "content", $("#lessonExplain").html())
            .tooltip("option", "tooltipClass", "whiteTooltip lesson_faq_toolTip");

        $(".bundle_faq").tooltip("option", "content", $("#bundleExplain").html())
          .tooltip("option", "tooltipClass", "whiteTooltip lesson_faq_toolTip");


        $(".content_language_change_lesson").tooltip("option", "content", function () {
            return $(this).find('.hiddenToolTip').html();
        });

        // unmark if you want tooltip to stay open when hover the tooltip
        //            $(".content_language_change_lesson").on("tooltipclose", function (event, ui) {
        //                ui.tooltip.hover(
        //                function () {
        //                    $(this).stop(true).fadeTo(100, 1);
        //                    //.fadeIn("slow"); // doesn't work because of stop()
        //                },
        //                function () {
        //                    $(this).fadeOut("100", function () { $(this).remove(); })
        //                }
        //                );
        //            });


        /* Dialogs Setup */
        $(".addToBasket").click(function () {
          //  $("#dialog_addLeasson").dialog("option", "dialogClass", "dialog_white").dialog("open");
        });

        $(".orderDemo").click(function () {
            //$("#dialog_orderDemo").dialog("option", "dialogClass", "dialog_white").dialog("open");
        });                     
        


        //show teacher 
        var teacherName = getURLParameter('teacher');

        //do teacher retrieve here

        if (teacherName != undefined && $('#' + teacherName).length > 0) {
            $("#" + teacherName).show();
        }

        // sticky
        var s = $("#sticker");
        var pos = s.position();
        var stickermax = $(document).outerHeight() - $("#footer_container").outerHeight() - s.outerHeight() - 40; //40 value is the total of the top and bottom margin
        $(window).scroll(function () {
            var windowpos = $(window).scrollTop();

            if (windowpos >= pos.top && windowpos < stickermax) {
                s.attr("style", ""); //kill absolute positioning
                s.addClass("stick"); //stick it
            } else if (windowpos >= stickermax) {
                s.removeClass("stick"); //un-stick
                s.css({ position: "absolute", top: stickermax + "px" }); //set sticker right above the footer

            } else {
                s.removeClass("stick"); //top of page
            }
        });
    }

    function getURLParameter(name) {
        return decodeURIComponent((new RegExp('[?|&]' + name + '=' + '([^&;]+?)(&|#|;|$)').exec(location.search) || [, ""])[1].replace(/\+/g, '%20')) || null
    }

    _mymentor.addModule("toraCategorySearch", toraCategorySearch);
})();

