import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import App from "./App";
// Başlık yazı tipi programla birlikte gelir (internet gerekmez). Yalnızca Latin ve Türkçe harfler.
import "@fontsource/cormorant-garamond/latin-600.css";
import "@fontsource/cormorant-garamond/latin-ext-600.css";
import "@fontsource/cormorant-garamond/latin-700.css";
import "@fontsource/cormorant-garamond/latin-ext-700.css";
import "@fontsource/cormorant-garamond/latin-500-italic.css";
import "@fontsource/cormorant-garamond/latin-ext-500-italic.css";
import "./styles.css";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
