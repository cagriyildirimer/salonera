//! Salonera masaüstü programı. Ekranlar React ile çizilir; bu dosya yalnızca
//! "ana bilgisayar mı, diğer bilgisayar mı?" ayarını ve yerel sunucuyu yönetir.

use std::path::PathBuf;
use std::sync::Mutex;
use std::time::Duration;

use salonera_core::discovery::{self, FoundServer};
use salonera_core::{server, App};
use serde::{Deserialize, Serialize};
use tauri::{Manager, State};

const SERVER_PORT: u16 = 47800;

#[derive(Serialize, Deserialize, Clone, Default)]
#[serde(rename_all = "camelCase")]
struct AppConfig {
    /// "main" = veriler bu bilgisayarda; "client" = ana bilgisayara bağlanır; boş = ilk açılış.
    mode: Option<String>,
    server_url: Option<String>,
}

struct Shell {
    config_path: PathBuf,
    data_dir: PathBuf,
    config: Mutex<AppConfig>,
    server_started: Mutex<bool>,
}

impl Shell {
    fn save(&self, config: AppConfig) -> Result<AppConfig, String> {
        let text = serde_json::to_string_pretty(&config).map_err(|e| e.to_string())?;
        std::fs::write(&self.config_path, text).map_err(|e| format!("Ayar dosyası yazılamadı: {e}"))?;
        *self.config.lock().unwrap() = config.clone();
        Ok(config)
    }

    /// Ana bilgisayar sunucusunu (bir kez) başlatır: veritabanı, ağ sunucusu ve otomatik bulma.
    fn start_server(&self) -> Result<String, String> {
        let mut started = self.server_started.lock().unwrap();
        if !*started {
            let app = App::open(&self.data_dir).map_err(|e| format!("Veri dosyası açılamadı: {e}"))?;
            let name_app = app.clone();
            discovery::spawn_responder(SERVER_PORT, move || {
                name_app
                    .with_db(|c| salonera_core::auth::server_info(c))
                    .map(|i| i.business_name)
                    .unwrap_or_default()
            })
            .map_err(|e| format!("Ağ bulma servisi başlatılamadı: {e}"))?;
            tauri::async_runtime::spawn(async move {
                if let Err(error) = server::serve(app, SERVER_PORT).await {
                    eprintln!("Salonera sunucusu durdu: {error}");
                }
            });
            *started = true;
        }
        Ok(format!("http://127.0.0.1:{SERVER_PORT}"))
    }
}

#[tauri::command]
fn get_config(shell: State<'_, Shell>) -> AppConfig {
    shell.config.lock().unwrap().clone()
}

/// Bu bilgisayarı ana bilgisayar yapar ve sunucuyu başlatır.
#[tauri::command]
fn become_main(shell: State<'_, Shell>) -> Result<AppConfig, String> {
    let url = shell.start_server()?;
    shell.save(AppConfig { mode: Some("main".into()), server_url: Some(url) })
}

/// Ağdaki ana bilgisayara bağlanır.
#[tauri::command]
fn connect_to(shell: State<'_, Shell>, url: String) -> Result<AppConfig, String> {
    let url = url.trim().trim_end_matches('/').to_string();
    if !url.starts_with("http://") {
        return Err("Adres http:// ile başlamalıdır.".into());
    }
    shell.save(AppConfig { mode: Some("client".into()), server_url: Some(url) })
}

#[tauri::command]
fn reset_config(shell: State<'_, Shell>) -> Result<AppConfig, String> {
    shell.save(AppConfig::default())
}

/// Ağdaki Salonera ana bilgisayarlarını arar (yaklaşık 1,5 saniye).
#[tauri::command]
async fn find_servers() -> Result<Vec<FoundServer>, String> {
    tauri::async_runtime::spawn_blocking(|| discovery::find_servers(Duration::from_millis(1500)))
        .await
        .map_err(|e| e.to_string())?
        .map_err(|e| format!("Ağda arama yapılamadı: {e}"))
}

#[tauri::command]
fn machine_name() -> String {
    discovery::machine_name()
}

/// Veri ve yedek klasörünü Windows Gezgini'nde açar (yalnızca ana bilgisayarda anlamlı).
#[tauri::command]
fn open_data_folder(shell: State<'_, Shell>) -> Result<(), String> {
    std::process::Command::new("explorer").arg(&shell.data_dir).spawn().map(|_| ()).map_err(|e| e.to_string())
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .setup(|app| {
            let base = app.path().app_data_dir()?;
            std::fs::create_dir_all(&base)?;
            let config_path = base.join("ayarlar.json");
            let config: AppConfig = std::fs::read_to_string(&config_path)
                .ok()
                .and_then(|text| serde_json::from_str(&text).ok())
                .unwrap_or_default();
            let shell = Shell {
                config_path,
                data_dir: base.join("veri"),
                config: Mutex::new(config.clone()),
                server_started: Mutex::new(false),
            };
            if config.mode.as_deref() == Some("main") {
                if let Err(error) = shell.start_server() {
                    eprintln!("{error}");
                }
            }
            app.manage(shell);
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![
            get_config,
            become_main,
            connect_to,
            reset_config,
            find_servers,
            machine_name,
            open_data_folder
        ])
        .run(tauri::generate_context!())
        .expect("Salonera başlatılamadı");
}
