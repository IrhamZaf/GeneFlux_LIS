/**
 * User CRUD JS
 */

'use strict';

// Functions to handle the Delete User Sweet Alerts (Delete Confirmation)
function showDeleteConfirmation(userId) {
  event.preventDefault(); // prevent form submit
  const userName = document.querySelector(`.user-name-full-${userId}`).innerText;
  Swal.fire({
    title: 'Delete User',
    // Show the user the user name to be deleted
    html: `<p class="text-danger">Are you sure you want to delete user ?<br> <span class="fw-medium text-body">${userName}</span></p>`,
    icon: 'warning',
    showCancelButton: true,
    confirmButtonText: 'Delete',
    cancelButtonText: 'Cancel',
    customClass: {
      confirmButton: 'btn btn-primary waves-effect waves-light',
      cancelButton: 'btn btn-label-secondary waves-effect waves-light'
    }
  }).then(result => {
    if (result.isConfirmed) {
      const form = document.getElementById(userId + '-deleteForm');
      if (form) {
        submitFormAndSetSuccessFlag(form, 'successFlag');
      } else {
        console.error('Form element not found');
      }
    } else {
      Swal.fire({
        title: 'Cancelled',
        // Show the user that the user has not been deleted.
        html: `<p><span class="fw-medium text-primary">${userName}</span> has not been deleted!</p>`,
        icon: 'error',
        confirmButtonText: 'OK',
        customClass: {
          confirmButton: 'btn btn-success waves-effect waves-light'
        }
      });
    }
  });
}

// Function to submit the form and set the success flag (Set success flags for delete, create and update)
function submitFormAndSetSuccessFlag(form, flagName) {
  form.submit();
  sessionStorage.setItem(flagName, 'true');
}

(function () {
  // Function to set element attributes (asp-for)
  function setElementAttributes(element, attribute, value) {
    element.setAttribute(attribute, value);
  }

  // Function to set form attributes (route and action)
  function setFormAttributes(form, userId, handler) {
    const routeAttribute = 'asp-route-id';
    setElementAttributes(form, routeAttribute, userId);
    form.action = `/CRUD/UserCRUD?handler=${handler}&id=${userId}`;
  }

  // Sweet Alert Success Function (User Deleted/Created/Updated)
  function showSuccessAlert(message) {
    var name = message[0].toUpperCase() + message.slice(1);
    Swal.fire({
      title: name,
      text: `User ${message} Successfully!`,
      icon: 'success',
      confirmButtonText: 'Ok',
      confirmButton: false,
      customClass: {
        confirmButton: 'btn btn-success waves-effect waves-light'
      }
    });
  }

  // Function to check for success flag and display success message
  function checkAndShowSuccessAlert(flagName, successMessage) {
    const flag = sessionStorage.getItem(flagName);
    if (flag === 'true') {
      showSuccessAlert(successMessage);
      sessionStorage.removeItem(flagName);
    }
  }

  // Function to handle the "Edit User" Offcanvas Modal
  const handleEditUserModal = editButton => {
    // Get the user details from the table
    const userId = editButton.id.split('-')[0];
    const userName = document.querySelector(`.user-name-full-${userId}`).innerText;
    const userEmail = document.getElementById(`${userId}-editUser`).parentElement.parentElement.children[3].innerText;
    const isVerified = document.querySelector(`.user-verified-${userId}`).dataset.isVerified;
    const userContactNumber = document.getElementById(`${userId}-editUser`).parentElement.parentElement.children[5]
      .innerText;
    const userSelectedRole = document.getElementById(`${userId}-editUser`).parentElement.parentElement.children[6]
      .innerText;
    const userSelectedPlan = document.getElementById(`${userId}-editUser`).parentElement.parentElement.children[7]
      .innerText;

    // Set the form attributes (route and action)
    const editForm = document.getElementById('editUserForm');
    setFormAttributes(editForm, userId, 'EditOrUpdate');

    // Set the input asp-for attributes (for model binding)
    setElementAttributes(document.getElementById('EditUser_UserName'), 'asp-for', `Users[${userId}].UserName`);
    setElementAttributes(document.getElementById('EditUser_Email'), 'asp-for', `Users[${userId}].Email`);
    setElementAttributes(document.getElementById('EditUser_IsVerified'), 'asp-for', `Users[${userId}].IsVerified`);
    setElementAttributes(
      document.getElementById('EditUser_ContactNumber'),
      'asp-for',
      `Users[${userId}].ContactNumber`
    );
    setElementAttributes(document.getElementById('EditUser_SelectedRole'), 'asp-for', `Users[${userId}].SelectedRole`);
    setElementAttributes(document.getElementById('EditUser_SelectedPlan'), 'asp-for', `Users[${userId}].SelectedPlan`);

    // Set the input values (for value binding)
    document.getElementById('EditUser_UserName').value = userName;
    document.getElementById('EditUser_Email').value = userEmail;
    document.getElementById('EditUser_IsVerified').checked = JSON.parse(isVerified.toLowerCase());
    document.getElementById('EditUser_ContactNumber').value = userContactNumber;
    document.getElementById('EditUser_SelectedRole').value = userSelectedRole.toLowerCase();
    document.getElementById('EditUser_SelectedPlan').value = userSelectedPlan.toLowerCase();
  };

  // Attach event listeners for "Edit User" buttons (pencil icon)
  const editUserButtons = document.querySelectorAll("[id$='-editUser']");
  editUserButtons.forEach(editButton => {
    editButton.addEventListener('click', () => handleEditUserModal(editButton));
  });

  // Check and Call the functions to check and display success messages on page reload (for delete, create and update)
  checkAndShowSuccessAlert('successFlag', 'Deleted');
  checkAndShowSuccessAlert('newUserFlag', 'Created');
  checkAndShowSuccessAlert('editUserFlag', 'Updated');

  // Get the Create for validation
  const createNewUserForm = document.getElementById('createUserForm');

  // Initialize FormValidation for create user form
  const fv = FormValidation.formValidation(createNewUserForm, {
    fields: {
      'NewUser.UserName': {
        validators: {
          notEmpty: {
            message: 'Please enter a user name'
          },
          stringLength: {
            min: 6,
            max: 20,
            message: 'The user name must be more than 6 and less than 20 characters long'
          }
        }
      },
      'NewUser.Email': {
        validators: {
          notEmpty: {
            message: 'Please enter an email address'
          },
          emailAddress: {
            message: 'Please enter a valid email address'
          },
          stringLength: {
            max: 50,
            message: 'The email address must be less than 50 characters long'
          }
        }
      },
      'NewUser.ContactNumber': {
        validators: {
          notEmpty: {
            message: 'Please enter a contact number'
          },
          phone: {
            country: 'US',
            message: 'Please enter a valid phone number'
          },
          stringLength: {
            min: 12,
            message: 'The contact number must be 10 characters long'
          }
        }
      },
      'NewUser.SelectedRole': {
        validators: {
          notEmpty: {
            message: 'Please select a role'
          }
        }
      },
      'NewUser.SelectedPlan': {
        validators: {
          notEmpty: {
            message: 'Please select a plan'
          }
        }
      }
    },
    plugins: {
      trigger: new FormValidation.plugins.Trigger(),
      bootstrap5: new FormValidation.plugins.Bootstrap5({
        eleValidClass: 'is-valid',
        rowSelector: function (field, ele) {
          return '.mb-6';
        }
      }),
      submitButton: new FormValidation.plugins.SubmitButton({
        // Specify the selector for your submit button
        button: '[type="submit"]'
      }),
      // Submit the form when all fields are valid
      // defaultSubmit: new FormValidation.plugins.DefaultSubmit(),
      autoFocus: new FormValidation.plugins.AutoFocus()
    }
  })
    .on('core.form.valid', function () {
      // if fields are valid then
      submitFormAndSetSuccessFlag(createNewUserForm, 'newUserFlag');
    })
    .on('core.form.invalid', function () {
      // if fields are invalid
      return;
    });

  // Phone mask initialization
  const phoneMaskList = document.querySelectorAll('.phone-mask');

  // Phone Number
  if (phoneMaskList) {
    phoneMaskList.forEach(function (phoneMask) {
      phoneMask.addEventListener('input', event => {
        const cleanValue = event.target.value.replace(/\D/g, '');
        phoneMask.value = formatGeneral(cleanValue, {
          blocks: [3, 3, 4],
          delimiters: [' ', ' ']
        });
      });
      registerCursorTracker({
        input: phoneMask,
        delimiter: ' '
      });
    });
  }

  // Get the Edit form validation
  const editUserForm = document.getElementById('editUserForm');

  // Initialize FormValidation for edit user form
  const fv2 = FormValidation.formValidation(editUserForm, {
    fields: {
      'user.UserName': {
        validators: {
          notEmpty: {
            message: 'Please enter a user name'
          },
          stringLength: {
            min: 6,
            max: 20,
            message: 'The user name must be more than 6 and less than 20 characters long'
          }
        }
      },
      'user.Email': {
        validators: {
          notEmpty: {
            message: 'Please enter an email address'
          },
          emailAddress: {
            message: 'Please enter a valid email address'
          },
          stringLength: {
            max: 50,
            message: 'The email address must be less than 50 characters long'
          }
        }
      },
      'user.ContactNumber': {
        validators: {
          notEmpty: {
            message: 'Please enter a contact number'
          },
          phone: {
            country: 'US',
            message: 'Please enter a valid phone number'
          },
          stringLength: {
            min: 12,
            message: 'The contact number must be 10 characters long'
          }
        }
      },
      'user.SelectedRole': {
        validators: {
          notEmpty: {
            message: 'Please select a role'
          }
        }
      },
      'user.SelectedPlan': {
        validators: {
          notEmpty: {
            message: 'Please select a plan'
          }
        }
      }
    },
    plugins: {
      trigger: new FormValidation.plugins.Trigger(),
      bootstrap5: new FormValidation.plugins.Bootstrap5({
        eleValidClass: 'is-valid',
        rowSelector: function (field, ele) {
          return '.mb-6';
        }
      }),
      submitButton: new FormValidation.plugins.SubmitButton({
        // Specify the selector for your submit button
        button: '[type="submit"]'
      }),
      // Submit the form when all fields are valid
      // defaultSubmit: new FormValidation.plugins.DefaultSubmit(),
      autoFocus: new FormValidation.plugins.AutoFocus()
    }
  })
    .on('core.form.valid', function () {
      // if fields are valid then
      submitFormAndSetSuccessFlag(editUserForm, 'editUserFlag');
    })
    .on('core.form.invalid', function () {
      // if fields are invalid
      return;
    });
})();

// User DataTable initialization
document.addEventListener('DOMContentLoaded', function (e) {
  let borderColor, bodyBg, headingColor;

  borderColor = config.colors.borderColor;
  bodyBg = config.colors.bodyBg;
  headingColor = config.colors.headingColor;

  // Variable declaration for table
  const dt_user_table = document.querySelector('#userTable');
  // User List DataTable Initialization (For User CRUD Page)
  if (dt_user_table) {
    const dt_user = new DataTable(dt_user_table, {
      order: [[1, 'asc']],
      layout: {
        topStart: {
          rowClass: 'row m-3 my-0 justify-content-between',
          features: [
            {
              pageLength: {
                menu: [7, 10, 20, 50, 70, 100],
                text: '_MENU_'
              }
            }
          ]
        },
        topEnd: {
          features: [
            {
              search: {
                placeholder: 'Search User',
                text: '_INPUT_'
              }
            },
            {
              buttons: [
                {
                  extend: 'collection',
                  className: 'btn btn-label-secondary dropdown-toggle',
                  text: '<i class="icon-base ti tabler-upload icon-xs me-2"></i>Export',
                  buttons: [
                    {
                      extend: 'print',
                      title: 'Users',
                      text: '<i class="icon-base ti tabler-printer me-2"></i>Print',
                      className: 'dropdown-item',
                      exportOptions: {
                        columns: [1, 2, 3, 4, 5],
                        // prevent avatar to be print
                        format: {
                          body: function (inner, coldex, rowdex) {
                            if (inner.length <= 0) return inner;

                            // Check if inner is HTML content
                            if (inner.indexOf('<') > -1) {
                              const parser = new DOMParser();
                              const doc = parser.parseFromString(inner, 'text/html');

                              // Get all text content
                              let text = '';

                              // Handle specific elements
                              const userNameElements = doc.querySelectorAll('.user-name');
                              if (userNameElements.length > 0) {
                                userNameElements.forEach(el => {
                                  // Get text from nested structure
                                  const nameText =
                                    el.querySelector('.fw-medium')?.textContent ||
                                    el.querySelector('.d-block')?.textContent ||
                                    el.textContent;
                                  text += nameText.trim() + ' ';
                                });
                              } else {
                                // Get regular text content
                                text = doc.body.textContent || doc.body.innerText;
                              }

                              return text.trim();
                            }

                            return inner;
                          }
                        }
                      },
                      customize: function (win) {
                        win.document.body.style.color = config.colors.headingColor;
                        win.document.body.style.borderColor = config.colors.borderColor;
                        win.document.body.style.backgroundColor = config.colors.bodyBg;
                        const table = win.document.body.querySelector('table');
                        table.classList.add('compact');
                        table.style.color = 'inherit';
                        table.style.borderColor = 'inherit';
                        table.style.backgroundColor = 'inherit';
                      }
                    },
                    {
                      extend: 'csv',
                      title: 'Users',
                      text: '<i class="icon-base ti tabler-file-text me-2" ></i>Csv',
                      className: 'dropdown-item',
                      exportOptions: {
                        columns: [1, 2, 3, 4, 5],
                        format: {
                          body: function (inner, coldex, rowdex) {
                            if (inner.length <= 0) return inner;

                            // Parse HTML content
                            const parser = new DOMParser();
                            const doc = parser.parseFromString(inner, 'text/html');

                            let text = '';

                            // Handle user-name elements specifically
                            const userNameElements = doc.querySelectorAll('.user-name');
                            if (userNameElements.length > 0) {
                              userNameElements.forEach(el => {
                                // Get text from nested structure - try different selectors
                                const nameText =
                                  el.querySelector('.fw-medium')?.textContent ||
                                  el.querySelector('.d-block')?.textContent ||
                                  el.textContent;
                                text += nameText.trim() + ' ';
                              });
                            } else {
                              // Handle other elements (status, role, etc)
                              text = doc.body.textContent || doc.body.innerText;
                            }

                            return text.trim();
                          }
                        }
                      }
                    },
                    {
                      extend: 'excel',
                      text: '<i class="icon-base ti tabler-file-spreadsheet me-2"></i>Excel',
                      className: 'dropdown-item',
                      exportOptions: {
                        columns: [1, 2, 3, 4, 5],
                        format: {
                          body: function (inner, coldex, rowdex) {
                            if (inner.length <= 0) return inner;

                            // Parse HTML content
                            const parser = new DOMParser();
                            const doc = parser.parseFromString(inner, 'text/html');

                            let text = '';

                            // Handle user-name elements specifically
                            const userNameElements = doc.querySelectorAll('.user-name');
                            if (userNameElements.length > 0) {
                              userNameElements.forEach(el => {
                                // Get text from nested structure - try different selectors
                                const nameText =
                                  el.querySelector('.fw-medium')?.textContent ||
                                  el.querySelector('.d-block')?.textContent ||
                                  el.textContent;
                                text += nameText.trim() + ' ';
                              });
                            } else {
                              // Handle other elements (status, role, etc)
                              text = doc.body.textContent || doc.body.innerText;
                            }

                            return text.trim();
                          }
                        }
                      }
                    },
                    {
                      extend: 'pdf',
                      title: 'Users',
                      text: '<i class="icon-base ti tabler-file-description me-2"></i>Pdf',
                      className: 'dropdown-item',
                      exportOptions: {
                        columns: [1, 2, 3, 4, 5],
                        format: {
                          body: function (inner, coldex, rowdex) {
                            if (inner.length <= 0) return inner;

                            // Parse HTML content
                            const parser = new DOMParser();
                            const doc = parser.parseFromString(inner, 'text/html');

                            let text = '';

                            // Handle user-name elements specifically
                            const userNameElements = doc.querySelectorAll('.user-name');
                            if (userNameElements.length > 0) {
                              userNameElements.forEach(el => {
                                // Get text from nested structure - try different selectors
                                const nameText =
                                  el.querySelector('.fw-medium')?.textContent ||
                                  el.querySelector('.d-block')?.textContent ||
                                  el.textContent;
                                text += nameText.trim() + ' ';
                              });
                            } else {
                              // Handle other elements (status, role, etc)
                              text = doc.body.textContent || doc.body.innerText;
                            }

                            return text.trim();
                          }
                        }
                      }
                    },
                    {
                      extend: 'copy',
                      title: 'Users',
                      text: '<i class="icon-base ti tabler-copy me-2" ></i>Copy',
                      className: 'dropdown-item',
                      exportOptions: {
                        columns: [1, 2, 3, 4, 5],
                        format: {
                          body: function (inner, coldex, rowdex) {
                            if (inner.length <= 0) return inner;

                            // Parse HTML content
                            const parser = new DOMParser();
                            const doc = parser.parseFromString(inner, 'text/html');

                            let text = '';

                            // Handle user-name elements specifically
                            const userNameElements = doc.querySelectorAll('.user-name');
                            if (userNameElements.length > 0) {
                              userNameElements.forEach(el => {
                                // Get text from nested structure - try different selectors
                                const nameText =
                                  el.querySelector('.fw-medium')?.textContent ||
                                  el.querySelector('.d-block')?.textContent ||
                                  el.textContent;
                                text += nameText.trim() + ' ';
                              });
                            } else {
                              // Handle other elements (status, role, etc)
                              text = doc.body.textContent || doc.body.innerText;
                            }

                            return text.trim();
                          }
                        }
                      }
                    }
                  ]
                },
                {
                  text: '<i class="icon-base ti tabler-plus icon-sm me-0 me-sm-2"></i><span class="d-none d-sm-inline-block">Add New User</span>',
                  className: 'add-new btn btn-primary',
                  attr: {
                    'data-bs-toggle': 'offcanvas',
                    'data-bs-target': '#createUserOffcanvas'
                  }
                }
              ]
            }
          ]
        },
        bottomStart: {
          rowClass: 'row mx-3 mb-4 justify-content-between',
          features: [
            {
              info: {
                text: 'Showing _START_ to _END_ of _TOTAL_ entries'
              }
            }
          ]
        },
        bottomEnd: 'paging'
      },
      displayLength: 7,
      language: {
        paginate: {
          next: '<i class="icon-base ti tabler-chevron-right scaleX-n1-rtl icon-18px"></i>',
          previous: '<i class="icon-base ti tabler-chevron-left scaleX-n1-rtl icon-18px"></i>',
          first: '<i class="icon-base ti tabler-chevrons-left scaleX-n1-rtl icon-18px"></i>',
          last: '<i class="icon-base ti tabler-chevrons-right scaleX-n1-rtl icon-18px"></i>'
        }
      },
      responsive: true,
      // For responsive popup
      rowReorder: {
        selector: 'td:nth-child(2)'
      },
      // For responsive popup button and responsive priority for user name
      columnDefs: [
        {
          // For Responsive Popup Button (plus icon)
          className: 'control',
          searchable: false,
          orderable: false,
          responsivePriority: 2,
          targets: 0,
          render: function (data, type, full, meta) {
            return '';
          }
        },
        {
          // For Id
          targets: 1,
          responsivePriority: 4
        },
        {
          // For User Name
          targets: 2,
          responsivePriority: 3
        },
        {
          // For Email
          targets: 3,
          responsivePriority: 9
        },
        {
          // For Is Verified
          targets: 4,
          responsivePriority: 5
        },
        {
          // For Contact Number
          targets: 5,
          responsivePriority: 7
        },
        {
          // For Role
          targets: 6,
          responsivePriority: 6
        },
        {
          // For Plan
          targets: 7,
          responsivePriority: 8
        },
        {
          // For Actions
          targets: -1,
          searchable: false,
          orderable: false,
          responsivePriority: 1
        }
      ],
      responsive: {
        details: {
          display: $.fn.dataTable.Responsive.display.modal({
            header: function (row) {
              var data = row.data();
              var $content = $(data[2]);
              // Extract the value of data-user-name attribute (User Name)
              var userName = $content.find('[class^="user-name-full-"]').text();
              return 'Details of ' + userName;
            }
          }),
          type: 'column',
          renderer: function (api, rowIdx, columns) {
            var data = $.map(columns, function (col, i) {
              // Exclude the last column (Action)
              if (i < columns.length - 1) {
                return col.title !== ''
                  ? '<tr data-dt-row="' +
                      col.rowIndex +
                      '" data-dt-column="' +
                      col.columnIndex +
                      '">' +
                      '<td>' +
                      col.title +
                      ':' +
                      '</td> ' +
                      '<td>' +
                      col.data +
                      '</td>' +
                      '</tr>'
                  : '';
              }
              return '';
            }).join('');

            return data ? $('<table class="table"/><tbody />').append(data) : false;
          }
        }
      }
    });
  }
  // Filter form control to default size
  setTimeout(() => {
    const elementsToModify = [
      { selector: '.dt-buttons .btn', classToRemove: 'btn-secondary' },
      { selector: '.dt-search .form-control', classToRemove: 'form-control-sm' },
      { selector: '.dt-length .form-select', classToRemove: 'form-select-sm', classToAdd: 'ms-0' },
      { selector: '.dt-length', classToAdd: 'mb-md-6 mb-0' },
      {
        selector: '.dt-layout-end',
        classToRemove: 'justify-content-between',
        classToAdd: 'd-flex gap-md-4 justify-content-md-between justify-content-center gap-2 flex-wrap'
      },
      { selector: '.dt-buttons', classToAdd: 'd-flex gap-4 mb-md-0 mb-4' },
      { selector: '.dt-layout-table', classToRemove: 'row mt-2' },
      { selector: '.dt-layout-full', classToRemove: 'col-md col-12' },
      { selector: '.datatables-users', classToAdd: 'table-responsive' }
    ];

    // Delete record
    elementsToModify.forEach(({ selector, classToRemove, classToAdd }) => {
      document.querySelectorAll(selector).forEach(element => {
        if (classToRemove) {
          classToRemove.split(' ').forEach(className => element.classList.remove(className));
        }
        if (classToAdd) {
          classToAdd.split(' ').forEach(className => element.classList.add(className));
        }
      });
    });
  }, 100);
});

// For Modal to close on edit button click
var editUserOffcanvas = $('#editUserOffcanvas');

// Event listener for the "Edit" offcanvas opening
editUserOffcanvas.on('show.bs.offcanvas', function () {
  // Close any open modals
  $('.modal').modal('hide');
});
