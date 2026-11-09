import type { FoundServer, ShellConfig } from "./types";

// Tauri içinde çalışırken Rust komutları kullanılır. Tarayıcıda geliştirme yaparken
// (npm run dev) aynı işlevler localStorage ile taklit edilir.
const inTauri = typeof window !== "undefined" && "__TAURI_INTERNALS__" in window;

async function invoke<R>(command: string, args?: Record<string, unknown>): Promise<R> {
  const { invoke } = await import("@tauri-apps/api/core");
  return invoke<R>(command, args);
}

const DEV_KEY = "salonera-dev-config";
const devConfig = (): ShellConfig => JSON.parse(localStorage.getItem(DEV_KEY) ?? '{"mode":null,"serverUrl":null}');
const saveDev = (config: ShellConfig) => {
  localStorage.setItem(DEV_KEY, JSON.stringify(config));
  return config;
};

export const shell = {
  inTauri,
  getConfig: () => (inTauri ? invoke<ShellConfig>("get_config") : Promise.resolve(devConfig())),
  becomeMain: () =>
    inTauri
      ? invoke<ShellConfig>("become_main")
      : Promise.resolve(saveDev({ mode: "main", serverUrl: "http://127.0.0.1:47800" })),
  connectTo: (url: string) =>
    inTauri ? invoke<ShellConfig>("connect_to", { url }) : Promise.resolve(saveDev({ mode: "client", serverUrl: url })),
  reset: () => (inTauri ? invoke<ShellConfig>("reset_config") : Promise.resolve(saveDev({ mode: null, serverUrl: null }))),
  findServers: () => (inTauri ? invoke<FoundServer[]>("find_servers") : Promise.resolve([])),
  machineName: () => (inTauri ? invoke<string>("machine_name") : Promise.resolve("Bu bilgisayar")),
  openDataFolder: () => (inTauri ? invoke<void>("open_data_folder") : Promise.resolve()),
};
