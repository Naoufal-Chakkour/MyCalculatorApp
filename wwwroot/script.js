// ============================================================
// TABS
// ============================================================

function switchTab(tab) {

    document
        .querySelectorAll('.tab-btn')
        .forEach(button =>
            button.classList.remove('active')
        );

    document
        .querySelectorAll('.tab-content')
        .forEach(content =>
            content.classList.remove('active')
        );

    const button =
        document.querySelector(
            `.tab-btn[onclick="switchTab('${tab}')"]`
        );

    if (button)
        button.classList.add('active');

    const map = {
        calc: 'calcTab',
        quad: 'quadTab',
        poly: 'polyTab'
    };

    document
        .getElementById(map[tab])
        .classList.add('active');
}


// ============================================================
// NORMAL CALCULATOR
// ============================================================

function getDisplay() {
    return document.getElementById('display');
}

function insert(text) {
    getDisplay().value += text;
}

function insertFunc(name) {
    getDisplay().value += name + '(';
}

function backspace() {

    const display = getDisplay();

    display.value =
        display.value.slice(0, -1);
}

function clearAll() {
    getDisplay().value = '';
}

function toggleAdvanced() {

    const panel =
        document.getElementById('advancedOps');

    const arrow =
        document.getElementById('arrow');

    panel.classList.toggle('open');

    arrow.textContent =
        panel.classList.contains('open')
            ? '▲'
            : '▼';
}


async function calculate() {

    const expr =
        getDisplay().value.trim();

    if (!expr)
        return;

    try {

        const response =
            await fetch('/calculate', {

                method: 'POST',

                body:
                    new URLSearchParams({
                        expr
                    }),

                headers: {
                    'Content-Type':
                        'application/x-www-form-urlencoded'
                }
            });

        if (!response.ok)
            throw new Error();

        getDisplay().value =
            await response.text();

    }
    catch {

        getDisplay().value =
            'خطأ';
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

    const a =
        document.getElementById('coefA')
            .value.trim() || '0';

    const b =
        document.getElementById('coefB')
            .value.trim() || '0';

    const c =
        document.getElementById('coefC')
            .value.trim() || '0';

    const result =
        document.getElementById('quadResult');

    result.value = 'جاري الحل...';

    try {

        const response =
            await fetch('/solve-quadratic', {

                method: 'POST',

                body:
                    new URLSearchParams({
                        a,
                        b,
                        c
                    }),

                headers: {
                    'Content-Type':
                        'application/x-www-form-urlencoded'
                }
            });

        if (!response.ok)
            throw new Error();

        const data =
            await response.json();

        /*
         * يدعم كلا الشكلين:
         *
         * camelCase:
         * data.radical
         *
         * PascalCase:
         * data.Radical
         *
         * وهذا يجعل الواجهة تعمل سواء غيّرت
         * إعدادات JSON في Program.cs أم لا.
         */

        quadRadicalText =
            data.radical ??
            data.Radical ??
            'خطأ';

        quadDecimalText =
            data.decimalVal ??
            data.DecimalVal ??
            'خطأ';

        quadStepsText =
            data.steps ??
            data.Steps ??
            '';

        quadShowingDecimal = false;

        result.value =
            quadRadicalText;

    }
    catch {

        result.value =
            'حدث خطأ أثناء الاتصال بالخادم.';

        quadRadicalText = '';
        quadDecimalText = '';
        quadStepsText = '';
    }
}


function toggleQuadDecimal() {

    quadShowingDecimal =
        !quadShowingDecimal;

    document.getElementById(
        'quadResult'
    ).value =
        quadShowingDecimal
            ? quadDecimalText
            : quadRadicalText;
}


// ============================================================
// GENERAL EQUATION
// ============================================================

let polyStepsText = '';

function getPolyDisplay() {
    return document.getElementById(
        'polyDisplay'
    );
}


function insertPoly(text) {

    getPolyDisplay().value += text;
}


function backspacePoly() {

    const display =
        getPolyDisplay();

    display.value =
        display.value.slice(0, -1);
}


function clearPoly() {

    getPolyDisplay().value = '';

    document.getElementById(
        'polyResult'
    ).value = '';

    polyStepsText = '';
}


function setExample(example) {

    getPolyDisplay().value =
        example;

    document.getElementById(
        'polyResult'
    ).value = '';

    polyStepsText = '';
}


async function solvePolynomialExpr() {

    const expr =
        getPolyDisplay()
            .value.trim();

    const result =
        document.getElementById(
            'polyResult'
        );

    if (!expr) {

        result.value =
            'اكتب معادلة أولاً.';

        return;
    }

    result.value =
        'جاري تحليل المعادلة...';

    try {

        const response =
            await fetch(
                '/solve-polynomial-expr',
                {
                    method: 'POST',

                    body:
                        new URLSearchParams({
                            expr
                        }),

                    headers: {
                        'Content-Type':
                            'application/x-www-form-urlencoded'
                    }
                }
            );

        if (!response.ok)
            throw new Error();

        const data =
            await response.json();

        /*
         * Program.cs قد يرجع:
         *
         * { "Result": "...", "Steps": "..." }
         *
         * أو:
         *
         * { "result": "...", "steps": "..." }
         *
         * لذلك ندعم الاثنين.
         */

        const solution =
            data.result ??
            data.Result ??
            '';

        const steps =
            data.steps ??
            data.Steps ??
            '';

        result.value =
            solution ||
            'تعذر إيجاد الحل.';

        polyStepsText =
            steps;

    }
    catch {

        result.value =
            'حدث خطأ أثناء الاتصال بالخادم.';

        polyStepsText = '';
    }
}


// ============================================================
// STEPS MODAL
// ============================================================

function openStepsModal(text) {

    if (!text) {

        text =
            'لا توجد خطوات بعد.\n\n' +
            'احسب المعادلة أولاً.';
    }

    document.getElementById(
        'stepsContent'
    ).textContent = text;

    document.getElementById(
        'stepsModal'
    ).classList.add('open');
}


function closeStepsModal() {

    document.getElementById(
        'stepsModal'
    ).classList.remove('open');
}


// ============================================================
// CLOSE MODAL BY CLICKING OUTSIDE
// ============================================================

document
    .getElementById('stepsModal')
    .addEventListener(
        'click',
        function (event) {

            if (event.target === this) {
                closeStepsModal();
            }
        }
    );


// ============================================================
// ESCAPE KEY
// ============================================================

document.addEventListener(
    'keydown',
    function (event) {

        if (event.key === 'Escape') {
            closeStepsModal();
        }
    }
);