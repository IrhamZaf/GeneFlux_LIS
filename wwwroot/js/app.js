window.downloadFileFromStream = async (fileName, contentStreamReference) => {
    const arrayBuffer = await contentStreamReference.arrayBuffer();
    const blob = new Blob([arrayBuffer], { type: 'application/pdf' });
    const url = URL.createObjectURL(blob);
    
    const anchorElement = document.createElement('a');
    anchorElement.href = url;
    anchorElement.setAttribute('download', fileName || 'report.pdf');
    
    document.body.appendChild(anchorElement);
    anchorElement.click();
    
    // Cleanup
    document.body.removeChild(anchorElement);
    URL.revokeObjectURL(url);
};

window.ensureVuexyShell = () => {
    document.documentElement.setAttribute("data-skin", "default");
    document.documentElement.setAttribute("data-bs-theme", "light");
    document.documentElement.classList.add("layout-navbar-fixed", "layout-menu-fixed", "layout-wide");
    document.body.classList.remove("vertical-sidebar-enable", "twocolumn-panel");

    // Submenu toggles use BOTH .menu-link and .menu-toggle; they must receive clicks.
    // Excluding .menu-toggle left those anchors with pointer-events:none (theme default), so
    // clicks fell through and triggered other sidebar controls (e.g. Settings below).
    document.querySelectorAll(".menu-link").forEach(link => {
        link.style.pointerEvents = "auto";
    });
};

window.ensureVuexyShell();

document.addEventListener("DOMContentLoaded", window.ensureVuexyShell);
document.addEventListener("enhancedload", window.ensureVuexyShell);

window.initReportDateRangePicker = (inputId, dotNetRef, startDate, endDate) => {
    setTimeout(() => {
        const $ = window.jQuery || window.$;
        if (typeof $ === 'undefined' || typeof window.moment === 'undefined' || typeof $.fn.daterangepicker === 'undefined') {
            console.error('Bootstrap Daterange Picker is not loaded.');
            return;
        }

        const $input = $(`#${inputId}`);
        if ($input.length === 0) return;

        if ($input.data('daterangepicker')) {
            $input.data('daterangepicker').remove();
        }

        const hasRange = startDate && endDate;
        const start = hasRange ? window.moment(startDate, 'YYYY-MM-DD') : window.moment().subtract(29, 'days');
        const end = hasRange ? window.moment(endDate, 'YYYY-MM-DD') : window.moment();

        $input.daterangepicker({
            autoUpdateInput: false,
            startDate: start,
            endDate: end,
            opens: document.documentElement.getAttribute('dir') === 'rtl' ? 'left' : 'right',
            locale: {
                format: 'DD/MM/YYYY',
                applyLabel: 'Apply',
                cancelLabel: 'Clear'
            }
        }, (selectedStart, selectedEnd) => {
            $input.val(`${selectedStart.format('DD/MM/YYYY')} - ${selectedEnd.format('DD/MM/YYYY')}`);
            dotNetRef.invokeMethodAsync(
                'SetDateRangeFromPicker',
                selectedStart.format('YYYY-MM-DD'),
                selectedEnd.format('YYYY-MM-DD'));
        });

        if (hasRange) {
            $input.val(`${start.format('DD/MM/YYYY')} - ${end.format('DD/MM/YYYY')}`);
        } else {
            $input.val('');
        }

        $input.off('apply.daterangepicker.lis').on('apply.daterangepicker.lis', (ev, picker) => {
            $input.val(`${picker.startDate.format('DD/MM/YYYY')} - ${picker.endDate.format('DD/MM/YYYY')}`);
            dotNetRef.invokeMethodAsync(
                'SetDateRangeFromPicker',
                picker.startDate.format('YYYY-MM-DD'),
                picker.endDate.format('YYYY-MM-DD'));
        });

        $input.off('cancel.daterangepicker.lis').on('cancel.daterangepicker.lis', () => {
            $input.val('');
            dotNetRef.invokeMethodAsync('SetDateRangeFromPicker', null, null);
        });
    }, 100);
};

window.destroyReportDateRangePicker = (inputId) => {
    const $ = window.jQuery || window.$;
    if (typeof $ === 'undefined') return;
    const $input = $(`#${inputId}`);
    if ($input.length && $input.data('daterangepicker')) {
        $input.data('daterangepicker').remove();
    }
};

window.initAdminDashboard = (topTests, statusBreakdown, monthlyTrend, hospitalVolume) => {
    setTimeout(() => {
        if (typeof ApexCharts === 'undefined') return;

        const isDark = document.documentElement.getAttribute("data-bs-theme") === "dark";
        const labelColor = isDark ? '#b6bee3' : '#5d596c';
        const borderColor = isDark ? '#434968' : '#dbdade';
        const cardBg = isDark ? '#2f3349' : '#fff';
        const mutedPalette = ['#7c90a8', '#8cb5a0', '#a48dc0', '#8fa8b8', '#b8a68a', '#7eb8c5'];

        // Top-5 Tests bar chart
        const topTestsEl = document.querySelector('#adminTopTestsChart');
        if (topTestsEl && topTests && topTests.length > 0) {
            topTestsEl.innerHTML = '';
            new ApexCharts(topTestsEl, {
                series: [{ name: 'Reports', data: topTests.map(t => t.count) }],
                chart: { type: 'bar', height: 320, toolbar: { show: false }, fontFamily: 'Public Sans', background: 'transparent' },
                plotOptions: { bar: { borderRadius: 6, horizontal: true, distributed: true, barHeight: '65%' } },
                dataLabels: { enabled: true, style: { colors: ['#fff'], fontSize: '12px' }, offsetX: -4 },
                xaxis: { categories: topTests.map(t => t.testName), labels: { style: { colors: labelColor, fontSize: '12px' } } },
                yaxis: { labels: { style: { colors: labelColor, fontSize: '12px' }, maxWidth: 200 } },
                grid: { borderColor: borderColor, strokeDashArray: 3 },
                colors: mutedPalette,
                legend: { show: false },
                tooltip: { y: { formatter: v => v + ' reports' } }
            }).render();
        }

        // Status donut
        const statusEl = document.querySelector('#adminStatusChart');
        if (statusEl && statusBreakdown && statusBreakdown.length > 0) {
            statusEl.innerHTML = '';
            const statusColors = { Draft: '#ff9f43', PendingReview: '#00bad1', Approved: '#28c76f', Archived: '#8592a3' };
            new ApexCharts(statusEl, {
                series: statusBreakdown.map(s => s.count),
                chart: { type: 'donut', height: 300, fontFamily: 'Public Sans', background: 'transparent' },
                labels: statusBreakdown.map(s => s.label.replace('PendingReview', 'Pending Review')),
                colors: statusBreakdown.map(s => statusColors[s.label] || '#8592a3'),
                stroke: { width: 2, colors: [cardBg] },
                legend: { show: true, position: 'bottom', labels: { colors: labelColor }, fontSize: '12px' },
                dataLabels: { enabled: false },
                plotOptions: {
                    pie: { donut: { size: '72%', labels: { show: true,
                        value: { fontSize: '1.4rem', color: labelColor, formatter: v => parseInt(v) },
                        total: { show: true, label: 'Total', fontSize: '0.8rem', color: labelColor,
                            formatter: w => w.globals.seriesTotals.reduce((a, b) => a + b, 0) }
                    }}}
                },
                tooltip: { y: { formatter: v => v + ' reports' } }
            }).render();
        }

        // Monthly trend area chart
        const trendEl = document.querySelector('#adminTrendChart');
        if (trendEl && monthlyTrend && monthlyTrend.length > 0) {
            trendEl.innerHTML = '';
            new ApexCharts(trendEl, {
                series: [{ name: 'Reports', data: monthlyTrend.map(m => m.count) }],
                chart: { type: 'area', height: 220, toolbar: { show: false }, fontFamily: 'Public Sans',
                    sparkline: { enabled: false }, background: 'transparent' },
                stroke: { width: 2.5, curve: 'smooth' },
                fill: { type: 'gradient', gradient: { shadeIntensity: 1, opacityFrom: 0.35, opacityTo: 0.08, stops: [0, 95, 100] } },
                xaxis: { categories: monthlyTrend.map(m => m.month), labels: { style: { colors: labelColor, fontSize: '11px' } },
                    axisBorder: { show: false }, axisTicks: { show: false } },
                yaxis: { labels: { style: { colors: labelColor }, formatter: v => Math.round(v) } },
                grid: { borderColor: borderColor, strokeDashArray: 3, padding: { bottom: 0 } },
                colors: ['#7367f0'],
                tooltip: { y: { formatter: v => v + ' reports' } },
                markers: { size: 4, strokeWidth: 2, hover: { size: 6 } }
            }).render();
        }

        // Hospital volume horizontal bar
        const hospEl = document.querySelector('#adminHospitalChart');
        if (hospEl && hospitalVolume && hospitalVolume.length > 0) {
            hospEl.innerHTML = '';
            new ApexCharts(hospEl, {
                series: [{ name: 'Reports', data: hospitalVolume.map(h => h.count) }],
                chart: { type: 'bar', height: 260, toolbar: { show: false }, fontFamily: 'Public Sans', background: 'transparent' },
                plotOptions: { bar: { borderRadius: 5, horizontal: true, distributed: true, barHeight: '60%' } },
                dataLabels: { enabled: true, style: { colors: ['#fff'], fontSize: '11px' } },
                xaxis: { categories: hospitalVolume.map(h => h.hospitalName), labels: { style: { colors: labelColor, fontSize: '11px' } } },
                yaxis: { labels: { style: { colors: labelColor, fontSize: '11px' }, maxWidth: 180 } },
                grid: { borderColor: borderColor, strokeDashArray: 3 },
                colors: ['#28c76f', '#7367f0', '#00bad1', '#ff9f43', '#ea5455', '#8592a3'],
                legend: { show: false },
                tooltip: { y: { formatter: v => v + ' reports' } }
            }).render();
        }
    }, 250);
};

window.initDashboardCharts = (hospitalData, statusData) => {
    // Small timeout to ensure DOM is ready and library is loaded
    setTimeout(() => {
        if (typeof ApexCharts === 'undefined') {
            console.error('ApexCharts is not defined. Ensure the library is loaded.');
            return;
        }

        const isDark = document.documentElement.getAttribute("data-bs-theme") === "dark";
        const labelColor = isDark ? '#b6bee3' : '#5d596c';
        const borderColor = isDark ? '#434968' : '#dbdade';

        // Hospital Chart
        const hospitalChartEl = document.querySelector('#hospitalChart');
        if (hospitalChartEl && hospitalData && hospitalData.length > 0) {
            hospitalChartEl.innerHTML = ''; 
            const hospitalOptions = {
                series: [{
                    name: 'Reports',
                    data: hospitalData.map(h => h.count)
                }],
                chart: {
                    type: 'bar',
                    height: 350,
                    toolbar: { show: false },
                    fontFamily: 'Public Sans'
                },
                plotOptions: {
                    bar: {
                        borderRadius: 6,
                        horizontal: true,
                        distributed: true,
                        barHeight: '60%'
                    }
                },
                dataLabels: { enabled: true, style: { colors: ['#fff'] } },
                xaxis: {
                    categories: hospitalData.map(h => h.name),
                    labels: { style: { colors: labelColor } }
                },
                yaxis: {
                    labels: { style: { colors: labelColor } }
                },
                grid: { borderColor: borderColor },
                colors: ['#28c76f', '#7367f0', '#00bad1', '#ff9f43', '#ea5455'],
                legend: { show: false }
            };
            new ApexCharts(hospitalChartEl, hospitalOptions).render();
        }

        // Status Chart
        const statusChartEl = document.querySelector('#statusChart');
        if (statusChartEl && statusData && statusData.length > 0) {
            statusChartEl.innerHTML = '';
            const statusOptions = {
                series: statusData.map(s => s.count),
                chart: {
                    type: 'donut',
                    height: 350,
                    fontFamily: 'Public Sans'
                },
                labels: statusData.map(s => s.label),
                colors: ['#ff9f43', '#28c76f'],
                stroke: { width: 0 },
                legend: { 
                    show: true, 
                    position: 'bottom',
                    labels: { colors: labelColor }
                },
                dataLabels: { enabled: false },
                plotOptions: {
                    pie: {
                        donut: {
                            size: '75%',
                            labels: {
                                show: true,
                                value: {
                                    fontSize: '1.5rem',
                                    color: labelColor,
                                    formatter: val => parseInt(val)
                                },
                                total: {
                                    show: true,
                                    label: 'Total',
                                    color: labelColor,
                                    formatter: w => w.globals.seriesTotals.reduce((a, b) => a + b, 0)
                                }
                            }
                        }
                    }
                }
            };
            new ApexCharts(statusChartEl, statusOptions).render();
        }
    }, 200);
};
