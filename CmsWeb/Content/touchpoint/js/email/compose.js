﻿$(function () {
// ReSharper disable UseOfImplicitGlobalInFunctionScope

    $('#Recipients').select2();
    $('#Recipients').select2("readonly", true);

    var currentDiv = null;
    var currentDesign = null;

    $.clearFunction = undefined;
    $.addFunction = undefined;

    $.clearTemplateClass = function () {
        if (typeof $.clearFunction !== 'undefined') {
            $.clearFunction();
        }
    };

    $.addTemplateClass = function () {
        if (typeof $.addFunction !== 'undefined') {
            $.addFunction();
        }
    };

    window.displayEditor = function (div, design, useUnlayer) {
        currentDiv = div;
        currentDesign = design;
        if (useUnlayer) {
            $('#unlayer-editor-modal').modal('show');
        } else {
            $('#editor-modal').modal('show');
        }
    };

    // set these two lines to false will cause the editor to show on a mobile device. Expermental.
    var xsDevice = false;//$('.device-xs').is(':visible');
    var smDevice = false;//$('.device-sm').is(':visible');

    $('#editor-modal').on('shown.bs.modal', function () {
        if (!xsDevice && !smDevice) {
            if (CKEDITOR.instances['htmleditor'])
                CKEDITOR.instances['htmleditor'].destroy();

            CKEDITOR.env.isCompatible = true;
            CKEDITOR.plugins.addExternal('specialLink', '/content/touchpoint/lib/ckeditor/plugins/specialLink/', 'plugin.js');
            $.fn.modal.Constructor.prototype.enforceFocus = function () {
              var modalThis = this;
              $(document).on('focusin.modal', function (e) {
                // Fix for CKEditor + Bootstrap IE issue with dropdowns on the toolbar
                // Adding additional condition '$(e.target.parentNode).hasClass('cke_contents cke_reset')' to
                // avoid setting focus back on the modal window.
                if (modalThis.$element[0] !== e.target && !modalThis.$element.has(e.target).length
                    && $(e.target.parentNode).hasClass('cke_contents cke_reset')) {
                  modalThis.$element.focus();
                }
              });
            };

            CKEDITOR.replace('htmleditor', {
                height: 200,
                autoParagraph: false,
                fullPage: false,
                allowedContent: true,
                customConfig: '/Content/touchpoint/js/ckeditorconfig.js',
                extraPlugins: 'specialLink'
            });
        }
        var html = $(currentDiv).html();
        if (html === "Click here to edit content") {
            if (xsDevice || smDevice)
                $('#htmleditor').val("");
            else 
                CKEDITOR.instances['htmleditor'].setData("");
        }
        else {
            if (xsDevice || smDevice) {
                $('#htmleditor').val(html);
            } else {
                CKEDITOR.instances['htmleditor'].setData(html);
            }
        }
    });
    $.fn.modal.Constructor.prototype.enforceFocus = function() {
        var modalThis = this;
        $(document).on('focusin.modal', function(e) {
            // Fix for CKEditor + Bootstrap IE issue with dropdowns on the toolbar
            // Adding additional condition '$(e.target.parentNode).hasClass('cke_contents cke_reset')' to
            // avoid setting focus back on the modal window.
            if (modalThis.$element[0] !== e.target && !modalThis.$element.has(e.target).length
                && $(e.target.parentNode).hasClass('cke_contents cke_reset')) {
                modalThis.$element.focus();
            }
        });
    };

    $('#editor-modal').on('click', '#cancel-edit', function () {
        if(!xsDevice && !smDevice)
            CKEDITOR.instances["htmleditor"].setData("");
        $('#editor-modal').modal('hide');
    });
    $('#editor-modal').on('click', '#save-edit', function () {
        var h;
        if (xsDevice || smDevice) {
            h = $('#htmleditor').val();
        } else {
            h = CKEDITOR.instances['htmleditor'].getData();
            CKEDITOR.instances["htmleditor"].setData("");
        }
        $(currentDiv).html(h);
        var eb = $('#email-body').contents().find('#templateBody').html();
        localStorage.email = eb;
        $('#editor-modal').modal('hide');
    });



    $('#unlayer-editor-modal').on('shown.bs.modal', function () {
        var design = $(currentDesign).val();
        
        unlayer.init({
            id: "unlayerEditor",
            displayMode: "email"
        });
        if (design.length > 0) {
            unlayer.loadDesign(JSON.parse(design));
        }
    });

    $('#unlayer-editor-modal').on('click', '#unlayer-cancel-edit', function () {
        $('#unlayerEditor').html('');
        $('#unlayer-editor-modal').modal('hide');
    });

    $('#unlayer-editor-modal').on('click', '#unlayer-save-edit', function () {
        unlayer.exportHtml(function(data) {
            var design = data.design;
            var html = data.html; // final html
            $(currentDesign).val(JSON.stringify(design));
            $(currentDiv).html(html);
            var eb = $('#email-body').contents().find('#templateBody').html();
            localStorage.email = eb;
            $('#unlayerEditor').html('');
            $('#unlayer-editor-modal').modal('hide');
        });
    });

    function sendEmail() {
        $.block();
        $('#body').val($('#email-body').contents().find('#templateBody').html());
        var q = $("#SendEmail").serialize();
        $.post('/Email/QueueEmails', q, function (ret) {
            if (ret && ret.error) {
                $.unblock();
                swal({
                    title: "Error!",
                    text: ret.error,
                    html: true,
                    type: "error"
                });
            } else {
                if (ret === "timeout") {
                    swal("Session Timeout!", 'Your session timed out. Please copy your email content and start over.', "error");
                    return;
                }
                var taskid = ret.id;
                if (taskid === 0) {
                    $.unblock();
                    swal({
                        title: 'Success!',
                        text: ret.content,
                        type: "success",
                        showCancelButton: false,
                    }, function () {
                        $('button.Send').prop('disabled', true);
                    });
                } else {
                    $("#send-actions").remove();
                    var intervalid = window.setInterval(function () {
                        $.post('/Email/TaskProgress/' + taskid, null, function (ret) {
                            $.unblock();
                            if (ret && ret.error) {
                                swal("Error!", ret.error, "error");
                                window.clearInterval(intervalid);
                            } else {
                                if (ret.title === 'Email has completed.') {
                                    swal({
                                        title: ret.title,
                                        text: ret.message,
                                        type: "success",
                                        showCancelButton: false,
                                    }, function () {
                                        $('button.Send').prop('disabled', true);
                                    });
                                    window.clearInterval(intervalid);
                                } else {
                                    swal({
                                        title: ret.title,
                                        text: ret.message,
                                        imageUrl: '/Content/touchpoint/img/spinner.gif'
                                    });
                                }
                            }
                        });
                    }, 3000);
                }
            }
        });
    }

    $(".Send").click(function () {
        if ($(this).attr('data-prompt') === 'True') {
            var count = $("#Count").val();
            swal({
                title: "Are you sure?",
                text: "You're about to send an email to " + count + " people.",
                type: "warning",
                showCancelButton: true,
                confirmButtonClass: "btn-confirm",
                confirmButtonText: "Yes, send it!",
                showLoaderOnConfirm: true,
                closeOnConfirm: false
            }, sendEmail);
        } else {
            sendEmail();
        }
    });

    $(".SaveDraft").click(function () {
        if ($(this).attr("saveType") === "0") {
            $('#draft-modal').modal('show');
        } else {
            $.clearTemplateClass();
            var d = $('#email-body').contents().find('#templateDesign').val();
            var h = $('#email-body').contents().find('#templateBody').html();
            $("#UnlayerDesign").val(d);
            $("#body").val(h);
            $("#name").val($("#newName").val());
            $.addTemplateClass();

            $("#SendEmail").attr("action", "/Email/SaveDraft");
            $("#SendEmail").submit();
        }
    });

    $('#draft-modal').on('shown.bs.modal', function () {
        $("#newName").val('').focus();
    });

    $("#SaveDraftButton").click(function () {
        $.clearTemplateClass();
        $("#UnlayerDesign").val($('#email-body').contents().find('#templateDesign').val());
        $("#body").val($('#email-body').contents().find('#templateBody').html());
        $("#name").val($("#newName").val());
        $.addTemplateClass();

        $("#SendEmail").attr("action", "/Email/SaveDraft");
        $("#SendEmail").submit();
    });

    $("#SaveTemplateButton").click(function () {
        $.clearTemplateClass();
        $("#UnlayerDesign").val($('#email-body').contents().find('#templateDesign').val());
        $("#body").val($('#email-body').contents().find('#templateBody').html());
        $.addTemplateClass();
    });
    $("#SaveTemplateCopyButton").click(function () {
        $.clearTemplateClass();
        $("#UnlayerDesign").val($('#email-body').contents().find('#templateDesign').val());
        $("#body").val($('#email-body').contents().find('#templateBody').html());
        $.addTemplateClass();
        var f = $(this).closest("form")[0];
        f.action = f.action + "Copy";
    });

    $(".TestSend").click(function () {
        $.block();

        $.clearTemplateClass();
        $("#body").val($('#email-body').contents().find('#templateBody').html());
        $.addTemplateClass();

        var q = $("#SendEmail").serialize();

        $.post('/Email/TestEmail', q, function (ret) {
            $.unblock();
            if (ret && ret.error) {
                swal("Error!", ret.error, "error");
            } else {
                if (ret === "timeout") {
                    swal("Session Timeout!", 'Your session timed out. Please copy your email content and start over.', "error");
                    return;
                }
                swal("Success!", ret, "success");
            }
        });
    });

    $('#Subject').focus();

});



