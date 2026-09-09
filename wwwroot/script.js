// ============================================================
// TABS
// ============================================================
function switchTab(tab) {
    document.querySelectorAll('.tab-btn').forEach(btn => btn.classList.remove('active'));
    document.querySelectorAll('.tab-content').forEach(c => c.classList.remove('active'));

    const btn = document.querySelector(`.tab-btn[onclick="switchTab('${tab}')"]`);
    if (btn) btn.classList.add('active');

    const map = { calc: 'calcTab', quad: 'quadTab', poly: 'polyTab' };
    document.getElementById(map[tab]).classList.add('active');

    closeAllDrawers();
}

// ============================================================
// الدرج الجانبي (يفتح من اليمين بدل التمدد للأسفل)
// ============================================================
function toggleDrawer(id) {
    const panel = document.getElementById(id);
    const backdrop = document.getElementById('drawerBackdrop');
    if (!panel || !backdrop) return;

    const isOpen = panel.classList.contains('open');

    document.querySelectorAll('.side-drawer').forEach(p => p.classList.remove('open'));

    if (!isOpen) {
        panel.classList.add('open');
        backdrop.classList.add('open');
    } else {
        backdrop.classList.remove('open');
    }
}

function closeAllDrawers() {
    document.querySelectorAll('.side-drawer').forEach(p => p.classList.remove('open'));
    const backdrop = document.getElementById('drawerBackdrop');
    if (backdrop) backdrop.classList.remove('open');
}

// ============================================================
// وضع الزاوية (درجة / راديان)
// ============================================================
let angleMode = 'deg';

function setAngleMode(mode) {
    angleMode = mode;
    document.getElementById('angleDegBtn').classList.toggle('active', mode === 'deg');
    document.getElementById('angleRadBtn').classList.toggle('active', mode === 'rad');
}

// ============================================================
// NORMAL CALCULATOR
// ============================================================
function getDisplay() { return document.getElementById('display'); }

function insert(text) {
    getDisplay().value += text;
}

// ملاحظة: sqrt يُدرج كرمز √ مباشرة (بدل كلمة sqrt) ليطابق سلوك تبويب "معادلة"
function insertFunc(name) {
    const display = getDisplay();
    if (name === 'sqrt') {
        display.value += '√(';
    } else {
        display.value += name + '(';
    }
}

function backspace() {
    const display = getDisplay();
    display.value = display.value.slice(0, -1);
}

function clearAll() {
    getDisplay().value = '';
}

function toggleAdvanced() {
    toggleDrawer('advancedOps');
}

async function calculate() {
    let expr = getDisplay().value.trim();
    if (!expr) return;
    // نعكس رمز الجذر √ إلى الكلمة sqrt قبل الإرسال للسيرفر (السيرفر يفهم sqrt فقط)
    expr = expr.replace(/√/g, 'sqrt');
    try {
        const response = await fetch('/calculate', {
            method: 'POST',
            body: new URLSearchParams({ expr, angleMode }),
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' }
        });
        if (!response.ok) throw new Error();
        getDisplay().value = await response.text();
    } catch {
        getDisplay().value = 'خطأ';
    }
}

// ============================================================
// QUADRATIC
// ============================================================
let quadShowingDecimal = false;
let quadRadicalText = '';
let quadDecimalText = '';
let quadStepsText = '';

async function solveQuadratic() {
    const a = document.getElementById('coefA').value.trim() || '0';
    const b = document.getElementById('coefB').value.trim() || '0';
    const c = document.getElementById('coefC').value.trim() || '0';
    const result = document.getElementById('quadResult');
    result.value = 'جاري الحل...';

    try {
        const response = await fetch('/solve-quadratic', {
            method: 'POST',
            body: new URLSearchParams({ a, b, c }),
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' }
        });
        if (!response.ok) throw new Error();
        const data = await response.json();

        quadRadicalText = data.radical ?? data.Radical ?? 'خطأ';
        quadDecimalText = data.decimalVal ?? data.DecimalVal ?? 'خطأ';
        quadStepsText = data.steps ?? data.Steps ?? '';

        quadShowingDecimal = false;
        result.value = quadRadicalText;
    } catch {
        result.value = 'حدث خطأ أثناء الاتصال بالخادم.';
        quadRadicalText = '';
        quadDecimalText = '';
        quadStepsText = '';
    }
}

function toggleQuadDecimal() {
    quadShowingDecimal = !quadShowingDecimal;
    document.getElementById('quadResult').value = quadShowingDecimal ? quadDecimalText : quadRadicalText;
}

// ============================================================
// GENERAL EQUATION (مع تحويل الأسس و √)
// ============================================================
let polyStepsText = '';

function getPolyDisplay() { return document.getElementById('polyDisplay'); }

// تحويل ^2 إلى ² و^3 إلى ³ و^x إلى ˣ للعرض فقط (السيرفر يعكسها تلقائيًا عند الإرسال)
function formatExponent(text) {
    return text
        .replace(/\^x/gi, 'ˣ')
        .replace(/\^(\d+)/g, (match, num) => {
            const superscripts = { '0': '⁰', '1': '¹', '2': '²', '3': '³', '4': '⁴', '5': '⁵', '6': '⁶', '7': '⁷', '8': '⁸', '9': '⁹' };
            return num.split('').map(d => superscripts[d] || d).join('');
        });
}

function insertPoly(text) {
    const display = getPolyDisplay();
    display.value += text;
    display.value = formatExponent(display.value);
}

// دالة خاصة لإدراج الدوال (تدرج اسم الدالة + قوس، مع دعم √)
function insertPolyFunc(name) {
    const display = getPolyDisplay();
    if (name === 'sqrt') {
        display.value += '√(';
    } else {
        display.value += name + '(';
    }
    display.value = formatExponent(display.value);
}

function backspacePoly() {
    const display = getPolyDisplay();
    display.value = display.value.slice(0, -1);
}

function clearPoly() {
    getPolyDisplay().value = '';
    document.getElementById('polyResult').value = '';
    polyStepsText = '';
}

function setExample(example) {
    getPolyDisplay().value = formatExponent(example);
    document.getElementById('polyResult').value = '';
    polyStepsText = '';
    closeAllDrawers();
}

async function solvePolynomialExpr() {
    let expr = getPolyDisplay().value.trim();
    const result = document.getElementById('polyResult');
    if (!expr) { result.value = 'اكتب معادلة أولاً.'; return; }
    // تحويل √ إلى sqrt للخادم (السيرفر يعكس ˣ و²/³/⁴/⁵ تلقائيًا فمرحلة التحليل)
    expr = expr.replace(/√/g, 'sqrt');

    result.value = 'جاري تحليل المعادلة...';

    try {
        const response = await fetch('/solve-polynomial-expr', {
            method: 'POST',
            body: new URLSearchParams({ expr, angleMode }),
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' }
        });
        if (!response.ok) throw new Error();
        const data = await response.json();

        const solution = data.result ?? data.Result ?? '';
        const steps = data.steps ?? data.Steps ?? '';
        result.value = solution || 'تعذر إيجاد الحل.';
        polyStepsText = steps;
    } catch {
        result.value = 'حدث خطأ أثناء الاتصال بالخادم.';
        polyStepsText = '';
    }
}

// ============================================================
// PLOT EQUATION (يدعم التربيعية والمعادلات)
// ============================================================
let chartInstance = null;

async function plotEquation() {
    const activeTab = document.querySelector('.tab-content.active');
    const chartContainer = document.getElementById('chartContainer');
    const canvas = document.getElementById('chartCanvas');
    let expr = '';

    if (activeTab.id === 'polyTab') {
        expr = getPolyDisplay().value.trim();
        if (!expr) { alert('اكتب معادلة أولاً.'); return; }
        // تحويل √ إلى sqrt للخادم
        expr = expr.replace(/√/g, 'sqrt');
    } else if (activeTab.id === 'quadTab') {
        const a = document.getElementById('coefA').value.trim() || '0';
        const b = document.getElementById('coefB').value.trim() || '0';
        const c = document.getElementById('coefC').value.trim() || '0';
        if (a === '0' && b === '0' && c === '0') {
            alert('أدخل معاملات a و b و c أولاً.');
            return;
        }
        let parts = [];
        if (a !== '0') parts.push(a + 'x^2');
        if (b !== '0') parts.push(b + 'x');
        if (c !== '0') parts.push(c);
        if (parts.length === 0) parts.push('0');
        expr = parts.join('+') + '=0';
        expr = expr.replace(/\+-/g, '-').replace(/^\+/, '');
    } else {
        alert('الرسم البياني متاح فقط في تبويب المعادلة والتربيعية.');
        return;
    }

    chartContainer.classList.add('open');

    try {
        const response = await fetch('/plot-equation', {
            method: 'POST',
            body: new URLSearchParams({ expr, angleMode }),
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' }
        });

        if (!response.ok) throw new Error('HTTP ' + response.status);
        const data = await response.json();
        if (data.error) {
            alert('خطأ: ' + data.error);
            chartContainer.classList.remove('open');
            return;
        }
        if (!data.x || data.x.length === 0) {
            alert('لا توجد بيانات كافية للرسم.');
            chartContainer.classList.remove('open');
            return;
        }

        document.getElementById('chartAnalysis').textContent = data.analysis || '';

        if (chartInstance) { chartInstance.destroy(); chartInstance = null; }

        const ctx = canvas.getContext('2d');
        const leftPoints = data.x.map((x, i) => ({ x, y: data.left[i] })).filter(p => p.y !== null && isFinite(p.y));
        const rightPoints = data.x.map((x, i) => ({ x, y: data.right[i] })).filter(p => p.y !== null && isFinite(p.y));

        const datasets = [
            { label: 'الطرف الأيسر', data: leftPoints, borderColor: '#14b8a6', backgroundColor: 'rgba(20,184,166,0.1)', borderWidth: 2, pointRadius: 0, tension: 0.3, showLine: true },
            { label: 'الطرف الأيمن', data: rightPoints, borderColor: '#f59e0b', backgroundColor: 'rgba(245,158,11,0.1)', borderWidth: 2, pointRadius: 0, tension: 0.3, showLine: true }
        ];

        if (data.solutionX !== null && !isNaN(data.solutionX) && data.solutionY !== null && !isNaN(data.solutionY)) {
            datasets.push({
                label: 'الحل',
                data: [{ x: data.solutionX, y: data.solutionY }],
                borderColor: '#ef4444',
                backgroundColor: '#ef4444',
                pointRadius: 10,
                pointStyle: 'circle',
                showLine: false,
                pointBorderColor: '#ffffff',
                pointBorderWidth: 2
            });
        }

        chartInstance = new Chart(ctx, {
            type: 'scatter',
            data: { datasets },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { labels: { color: '#ffffff', font: { size: 14 } } },
                    tooltip: {
                        callbacks: {
                            label: function(context) {
                                const raw = context.raw;
                                if (raw && typeof raw === 'object') {
                                    return context.dataset.label + ' (x=' + raw.x.toFixed(4) + ', y=' + raw.y.toFixed(4) + ')';
                                }
                                return context.dataset.label + ' (x=' + context.label + ', y=' + context.parsed.y.toFixed(4) + ')';
                            }
                        }
                    }
                },
                scales: {
                    x: { type: 'linear', position: 'bottom', grid: { color: '#444' }, ticks: { color: '#aaa' }, title: { display: true, text: 'x', color: '#aaa' } },
                    y: { grid: { color: '#444' }, ticks: { color: '#aaa' }, title: { display: true, text: 'y', color: '#aaa' } }
                }
            }
        });

    } catch (err) {
        alert('خطأ في الرسم: ' + err.message);
        chartContainer.style.display = 'none';
    }
}

// إغلاق الرسم البياني
function closeChart() {
    document.getElementById('chartContainer').classList.remove('open');
    if (chartInstance) {
        chartInstance.destroy();
        chartInstance = null;
    }
}

// ============================================================
// STEPS MODAL
// ============================================================
function openStepsModal(text) {
    if (!text) text = 'لا توجد خطوات بعد.\n\nاحسب المعادلة أولاً.';
    document.getElementById('stepsContent').textContent = text;
    document.getElementById('stepsModal').classList.add('open');
}

function closeStepsModal() {
    document.getElementById('stepsModal').classList.remove('open');
}

document.getElementById('stepsModal').addEventListener('click', function(event) {
    if (event.target === this) closeStepsModal();
});

document.addEventListener('keydown', function(event) {
    if (event.key === 'Escape') {
        closeStepsModal();
        closeAllDrawers();
    }
});