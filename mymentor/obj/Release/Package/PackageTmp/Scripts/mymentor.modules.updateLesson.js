(function () {
    var vm;
    updateLesson.prototype = new _mymentor.Module;
    updateLesson.prototype.base = _mymentor.Module.prototype;

    function lessonUpdateViewModel(data) {
        if (data == null) data = new Object();
        var self = this;
        this.isValid = ko.observable(true);
        this.ObjectId = ko.observable(data.ObjectId);
        this.PortalNamePart1 = ko.observable(data.PortaNamePart1);
        this.PortalNamePart2 = ko.observable(data.PortaNamePart2);
        this.IsRTL = ko.observable(data.IsRTL);

        this.CategoryName1 = ko.observable(data.CategoryName1);
        this.CategoryName2 = ko.observable(data.CategoryName2);
        this.CategoryName3 = ko.observable(data.CategoryName3);
        this.CategoryName4 = ko.observable(data.CategoryName4);

        this.Category1Values = ko.observableArray(data.Category1Values);
        this.Category2Values = ko.observableArray(data.Category2Values);
        this.Category3Values = ko.observableArray(data.Category3Values);
        this.Category4Values = ko.observableArray(data.Category4Values);

        this.SelectedCategory1Value = ko.observable(data.SelectedCategory1Value);
        this.SelectedCategory2Value = ko.observable(data.SelectedCategory2Value);
        this.SelectedCategory3Value = ko.observable(data.SelectedCategory3Value);
        this.SelectedCategory4Value = ko.observable(data.SelectedCategory4Value);

        this.getTextFromDropdown = function (selectedValue, values) {
            var selectedId = selectedValue;
            var text = $.grep(values, function (obj) {
                return obj.Key === selectedId;
            });

            if (text.length > 0) {
                return text[0].Value;
            }
            return "";
        }
        this.SelectedCategory1TextComputed = ko.computed(function () {
            return self.getTextFromDropdown(self.SelectedCategory1Value(), self.Category1Values());
        }, this);
        this.SelectedCategory2TextComputed = ko.computed(function () {
            return self.getTextFromDropdown(self.SelectedCategory2Value(), self.Category2Values());
        }, this);
        this.SelectedCategory3TextComputed = ko.computed(function () {
            return self.getTextFromDropdown(self.SelectedCategory3Value(), self.Category3Values());
        }, this);
        this.SelectedCategory4TextComputed = ko.computed(function () {
            return self.getTextFromDropdown(self.SelectedCategory4Value(), self.Category4Values());
        }, this);

        this.Category1VisibleComputed = ko.computed(function () {
            return self.CategoryName1() !== undefined && self.CategoryName1() != null && self.CategoryName1().length > 0;
        });
        this.Category2VisibleComputed = ko.computed(function () {
            return self.CategoryName2() !== undefined && self.CategoryName2() != null && self.CategoryName2().length > 0;
        });
        this.Category3VisibleComputed = ko.computed(function () {
            return self.CategoryName3() !== undefined && self.CategoryName3() != null && self.CategoryName3().length > 0;
        });
        this.Category4VisibleComputed = ko.computed(function () {
            return self.CategoryName4() !== undefined && self.CategoryName4() != null && self.CategoryName4().length > 0;
        });

        this.Created = ko.observable(data.Created);
        this.Updated = ko.observable(data.Updated);
        this.Version = ko.observable(data.Version);
        this.Keywords = ko.observable(data.Keywords);

        this.Remarks_he_il = ko.observable(data.Remarks_he_il);
        this.Remarks_en_us = ko.observable(data.Remarks_en_us);
        this.Performer_he_il = ko.observable(data.Performer_he_il);
        this.Performer_en_us = ko.observable(data.Performer_en_us);
        this.Description_he_il = ko.observable(data.Description_he_il);
        this.Description_en_us = ko.observable(data.Description_en_us);

        this.ReadingDates = ko.observableArray(data.ReadingDates);
        this.SelectedReadingDate = ko.observableArray(data.SelectedReadingDate);
        this.StatusValues = ko.observableArray(data.StatusValues);
        this.SelectedStatus = ko.observableArray(data.SelectedStatus);

        this.LessonPrice = ko.observable(data.LessonPrice);
        this.SupportPrice = ko.observable(data.SupportPrice == undefined ? 0 : data.SupportPrice);
        this.MinimumPrice = ko.observable(data.MinimumPrice);
        this.TeacherName = ko.observable(data.TeacherName);
        this.TeacherResidence = ko.observable(data.TeacherResidence);

        this.CurrencyId = ko.observable(data.Currency != undefined ? data.Currency.ObjectId : "");
        this.DefaultCurrencyId = ko.observable(data.DefaultCurrencyId);
        this.ComputedCategory2Value = ko.computed(function () {
            var category2Values = self.Category2Values();
            var selectedCat1Value = self.SelectedCategory1Value();
            var result = [];
            $.each(category2Values, function (key, value) {
                if (value.ParentCategoryId === selectedCat1Value) {
                    result.push(value);
                }
            });
            return result;

        }, this);
        this.ComputedMinPrice = ko.computed(function () {
            var selectedCategory3Value = self.SelectedCategory3Value();
            if (selectedCategory3Value === undefined) return;
            $.each(self.Category3Values(), function (index, category) {
                if (category.Key === selectedCategory3Value) {
                    if (self.DefaultCurrencyId() != "") {
                        new DataModel().getConvertedCurrency(self.DefaultCurrencyId(), category.AdditionalInfo[0].Value).done(function (d) {
                            self.MinimumPrice(d);
                        });
                    }
                }
            });

        }, this);
        this.ShowReadingDates = ko.observable(data.ShowReadingDates);
        this.TeacherFirstName = ko.observable(data.TeacherFirstName);
        this.TeacherLastName = ko.observable(data.TeacherLastName);

        this.LessonTitleTemplatePart1 = ko.observable(data.LessonTitleTemplatePart1);
        this.LessonTitleTemplatePart2 = ko.observable(data.LessonTitleTemplatePart2);
        this.lessonTitlePart1Computed = ko.computed(function () {
            var template = self.LessonTitleTemplatePart1();
            if (template === undefined || template === null) return "";
           
            var category2 = "[category2]";
            var title = template;
            title = title.replace(category2, self.SelectedCategory2TextComputed());

            self.PortalNamePart1(title);
            return title;
        }, this);
        this.lessonTitlePart2Computed = ko.computed(function () {
            var template = self.LessonTitleTemplatePart2();
            if (template === undefined || template === null) return "";
            var category1 = "[category1]";
            var category2 = "[category2]";
            var category3 = "[category3]";
            var category4 = "[category4]";
            var description = "[description]";
            var teacherResidence = "[cityOfResidence]";
            var remarks = "[remarks]";
            var teacherFirstName = "[firstName]";
            var teacherLastName = "[lastName]";

            var descriptionValueHebrew = self.Description_he_il() !== undefined && self.Description_he_il() != null ? self.Description_he_il() : "";
            var descriptionValueEnglish = self.Description_en_us() !== undefined && self.Description_en_us() != null ? self.Description_en_us() : "";
            var descriptionValue = self.IsRTL() ? descriptionValueHebrew : descriptionValueEnglish;

            var remarksValueHebew = self.Remarks_he_il() !== undefined && self.Remarks_he_il() != null ? self.Remarks_he_il() : "";
            var remarksValueEnglish = self.Remarks_en_us() !== undefined && self.Remarks_en_us() != null ? self.Remarks_en_us() : "";
            var remarksValue = self.IsRTL() ? remarksValueHebew : remarksValueEnglish;
            var firstName = self.TeacherName().split(' ')[0];
            var lastName = self.TeacherName().replace(firstName, '');

            var title = template;
            title = title.replace(category1, self.SelectedCategory1TextComputed());
            title = title.replace(category2, self.SelectedCategory2TextComputed());
            title = title.replace(category3, self.SelectedCategory3TextComputed());
            title = title.replace(category4, self.SelectedCategory4TextComputed());
            title = title.replace(description, descriptionValue);
            title = title.replace(remarks, remarksValue);
            title = title.replace(teacherResidence, self.TeacherResidence());
            title = title.replace(teacherFirstName, firstName);
            title = title.replace(teacherLastName, lastName);

            while (title.indexOf(",,") != -1) {
                title = title.replace(",,", ",");
                title = title.trim();
            }

            while (title.indexOf(', ,') != -1) {
                title = title.replace(", ,", ",");
                title = title.trim();
            }

            while (title.indexOf(",") == 0) {
                title = title.substring(1);
                title = title.trim();
            }

            while (title.indexOf(",") == title.length - 1) {
                title = title.substring(title.length - 1);
                title = title.trim();
            }

            self.PortalNamePart2(title);
            return title;
        }, this);

        this.ReadingDates.requiredError = ko.observable();
        this.ReadingDates.dateError = ko.observable();
        this.LessonPrice.requiredError = ko.observable();
        this.LessonPrice.numericError = ko.observable();
        this.LessonPrice.minPriceError = ko.observable();
        this.SupportPrice.numericError = ko.observable();
        

        this.validate = function () {
            var hasDateError = function () {
                var foundDateInPast = false;
                $.each(self.ReadingDates(), function(key, value) {
                    var parts = value.split('/');
                    var dateToCheck = new Date(parts[2],parts[1],parts[0]);
                    var today = new Date();
                    if (today > dateToCheck) {
                        foundDateInPast =  true;
                    }
                });

                return foundDateInPast;
            };

            self.ReadingDates.requiredError(self.ReadingDates().length === 0 && self.ShowReadingDates());
            self.ReadingDates.dateError(hasDateError());

            self.LessonPrice.requiredError(self.LessonPrice() === undefined || self.LessonPrice() === "");
            self.LessonPrice.numericError(isNaN(self.LessonPrice()));
            self.LessonPrice.minPriceError(parseFloat(self.LessonPrice()) < parseFloat(self.MinimumPrice()));
            self.SupportPrice.numericError(isNaN(self.SupportPrice()));

            var hasError = self.LessonPrice.requiredError() ||
                self.ReadingDates.requiredError() ||
                self.LessonPrice.numericError() ||
                self.SupportPrice.numericError() ||
                self.LessonPrice.minPriceError() ||
                self.ReadingDates.dateError();

            return !hasError;
        };
        this.submit = function () {
            self.TeacherFirstName(self.TeacherName().split(' ')[0]);
            self.TeacherLastName(self.TeacherName().replace(self.TeacherFirstName(), ''));

            var model = ko.toJSON(self);
            if (self.validate()) {
                new DataModel().setLessonData(model).done(function (response) {
                    if (response.Status == -1) {
                        alert(response.StatusReason);
                        eh.saveLessonClick();
                        return;
                    }
                    //
                    m.hideWait("#saveLesson");
                    $("#mtr-sucessMessage").dialog("option", "dialogClass", "dialog_white").dialog("open");
                    $(".btn_mentor").one("click", function() {
                        document.location.href = document.location.href;
                    });
                }).fail(function() {});
            } else {
                eh.saveLessonClick();
                vm.isValid(false);
            }
        }
    }

    var eh = {
        calendarInputs: function (vm) {

            $(".icon_calendar_input").datepicker({

            });            
            $("#icon_calendar_dialog_input").datepicker({
                onSelect: function (date) {
                    vm.ReadingDates.push(date);
                }
            });

            $("#icon_calendar_dialog_input").datepicker("option", $.datepicker.regional[$("#icon_calendar_dialog_input").data("regional")]);
            $("#icon_calendar_dialog_input").datepicker('option', 'dateFormat', 'dd/mm/yy');
            $("#icon_calendar_dialog_input").datepicker("option", "isRTL", $("#icon_calendar_dialog_input").data("isrtl") == "True");

            $(".icon_calendar_dialog").click(function () {
                $("#icon_calendar_dialog_input").datepicker('show');
            });

            $(".icon_remove_item").click(function () {
                var selected = vm.SelectedReadingDate();
                var readingDates = vm.ReadingDates();
                if (selected != null && selected.length > 0) {
                    $.each(selected, function (i, value) {
                        var index = readingDates.indexOf(value);
                        readingDates.splice(index, 1);
                        vm.ReadingDates(readingDates);
                    });
                }
            });
        },

        saveLessonClick: function () {
            m.hideWait("#saveLesson");
            $("#saveLesson").one("click", function () {
                m.showWait("#saveLesson");
                vm.submit();
            });
         }
    };

    function updateLesson(name, settings) {
        this.name = name;
    };

    updateLesson.prototype.init = function () {
        this.base.init.call(this);
    };

    updateLesson.prototype.onLoad = function () {
        this.base.onLoad.call(this);
       
        vm = new lessonUpdateViewModel();
        ko.applyBindings(vm, $("#dialog_editLesson")[0]);

        eh.calendarInputs(vm);
        eh.saveLessonClick();

        $(".editLesson").click(function (sender) {

            var clip = sender.currentTarget.dataset.itemId;
            $("body").css("cursor", "wait");
            $(".editLesson").css("cursor", "wait");

            new DataModel().getLessonData(clip).done(function (data) {
                $("body").css("cursor", "default");
                $(".editLesson").css("cursor", "default");

                vm.ObjectId(clip);
                vm.PortalNamePart1(data.PortalNamePart1);
                vm.PortalNamePart2(data.PortalNamePart2);
                vm.IsRTL(data.IsRTL);
                vm.ShowReadingDates(data.ShowReadingDates);

                vm.CategoryName1(data.CategoryName1);
                vm.CategoryName2(data.CategoryName2);
                vm.CategoryName3(data.CategoryName3);
                vm.CategoryName4(data.CategoryName4);

                vm.Category1Values(data.Category1Values);
                vm.Category2Values(data.Category2Values);
                vm.Category3Values(data.Category3Values);
                vm.Category4Values(data.Category4Values);

                vm.SelectedCategory1Value(data.SelectedCategory1Value);
                vm.SelectedCategory2Value(data.SelectedCategory2Value);
                vm.SelectedCategory3Value(data.SelectedCategory3Value);
                vm.SelectedCategory4Value(data.SelectedCategory4Value);

                vm.Created(data.Created);
                vm.Updated(data.Updated);
                vm.Version(data.Version);
                vm.Keywords(data.Keywords);

                vm.Remarks_he_il(data.Remarks_he_il);
                vm.Remarks_en_us(data.Remarks_en_us);
                vm.Performer_he_il(data.Performer_he_il);
                vm.Performer_en_us(data.Performer_en_us);
                vm.Description_he_il(data.Description_he_il);
                vm.Description_en_us(data.Description_en_us);

                vm.ReadingDates(data.ReadingDates);
                vm.SelectedReadingDate(data.SelectedReadingDate);
                vm.StatusValues(data.StatusValues);
                vm.SelectedStatus(data.SelectedStatus);

                vm.LessonPrice(data.LessonPrice);
                vm.SupportPrice(data.SupportPrice);
                vm.MinimumPrice(data.MinimumPrice);
                vm.TeacherName(data.TeacherName);
                vm.TeacherResidence(data.TeacherResidence);
                vm.CurrencyId(data.CurrencyId);
                vm.DefaultCurrencyId(data.DefaultCurrencyId);
                vm.LessonTitleTemplatePart1(data.LessonTitleTemplatePart1);
                vm.LessonTitleTemplatePart2(data.LessonTitleTemplatePart2);
                vm.TeacherFirstName(data.TeacherFirstName);
                vm.TeacherLastName(data.TeacherLastName);

                $("#dialog_editLesson").dialog("option", "dialogClass", "dialog_inputs").dialog("open");
            });
        });
        
    };


    var m = {
        showWait: function (selector) {
            $.blockUI({ message: $("#domMessage") });
        },
        hideWait: function (selector) {
            $.unblockUI();
        }
    }
    _mymentor.addModule("updateLesson", updateLesson);
})();
