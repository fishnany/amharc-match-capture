import { createRoot } from "react-dom/client";
import { setBaseUrl } from "@workspace/api-client-react";

import App from "./App";
import "./index.css";

const configuredAgentUrl =
  import.meta.env.VITE_AGENT_API_URL?.trim().replace(/\/+$/, "") ?? "";

setBaseUrl(configuredAgentUrl || null);

createRoot(document.getElementById("root")!).render(<App />);
