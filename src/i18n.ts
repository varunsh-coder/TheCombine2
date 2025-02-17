// https://dev.to/adrai/how-to-properly-internationalize-a-react-application-using-i18next-3hdb#getting-started
import i18n from "i18next";
import LanguageDetector from "i18next-browser-languagedetector";
import Backend from "i18next-http-backend";
import { initReactI18next } from "react-i18next";

import { getCurrentUser } from "backend/localStorage";
import { i18nFallbacks, i18nLangs } from "types/writingSystem";

// declare custom type options so the return is always a string.
declare module "i18next" {
  interface CustomTypeOptions {
    returnNull: false;
  }
}

i18n
  .use(Backend)
  .use(LanguageDetector)
  .use(initReactI18next)
  .init(
    {
      //debug: true, // Uncomment to troubleshoot
      returnNull: false,
      // detection: options,
      // ignoring localStorage and cookies for the detection order lets the user change languages
      // more easily (just switch in the browser and reload, instead of clearing all site data)
      detection: { order: ["queryString", "path", "navigator"] },
      supportedLngs: i18nLangs,
      // nonExplicitSupportedLngs will (e.g.) use 'es' if the browser is 'es-MX'
      nonExplicitSupportedLngs: true,
      fallbackLng: i18nFallbacks,
      interpolation: { escapeValue: false },
    },
    setDir // Callback function to set the direction ("ltr" vs "rtl") after i18n has initialized
  );

function setDir(): void {
  document.body.dir = i18n.dir();
}

export function updateLangFromUser(): void {
  const uiLang = getCurrentUser()?.uiLang;
  if (uiLang && uiLang !== i18n.resolvedLanguage) {
    i18n.changeLanguage(uiLang, setDir);
  }
}

export default i18n;
