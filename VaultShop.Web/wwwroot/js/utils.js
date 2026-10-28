window.culture = currentCulture;


window.SpanishNumberTables = function (culture) {
    if (culture == "es-AR") {
        return $.fn.dataTable.render.number('.', ',', 2, '$');
    } else {
        return $.fn.dataTable.render.number(',', '.', 2, '$');
    }
}
window.SpanishCultureTables = function (culture) {
    if (culture == "es-AR") {
        return {
            url: '/lib/datatables/i18n/es-AR.json'
        };
    }
}

const getStoredTheme = () => localStorage.getItem('theme')
const setStoredTheme = theme => localStorage.setItem('theme', theme)

const getPreferredTheme = () => {
    const storedTheme = getStoredTheme()
    if (storedTheme === 'light' || storedTheme === 'dark') {
        return storedTheme
    }

    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

const setTheme = theme => document.documentElement.setAttribute('data-bs-theme', theme)

const applyTheme = theme => {
    setTheme(theme)
    document.querySelectorAll('[data-theme-toggle]').forEach(toggle => {
        const isDark = theme === 'dark'
        toggle.setAttribute('aria-pressed', isDark ? 'true' : 'false')
        toggle.setAttribute('aria-label', isDark ? toggle.dataset.labelLight : toggle.dataset.labelDark)
    })
}

applyTheme(getPreferredTheme())

window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
    const storedTheme = getStoredTheme()
    if (storedTheme !== 'light' && storedTheme !== 'dark') {
        setTheme(getPreferredTheme())
    }
})

window.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('[data-theme-toggle]').forEach(toggle => {
        toggle.addEventListener('click', () => {
            const nextTheme = document.documentElement.getAttribute('data-bs-theme') === 'dark' ? 'light' : 'dark'
            setStoredTheme(nextTheme)
            applyTheme(nextTheme)
        })
    })
})


