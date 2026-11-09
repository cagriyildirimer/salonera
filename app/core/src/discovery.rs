//! Yerel ağda ana bilgisayarı otomatik bulma.
//!
//! Diğer bilgisayarlar ağa "SALONERA?" diye bir yayın mesajı gönderir; ana bilgisayar
//! kendi adresi, sunucu portu ve işletme adıyla cevap verir. Böylece kullanıcının IP adresi
//! yazması gerekmez.

use std::net::{SocketAddr, UdpSocket};
use std::time::{Duration, Instant};

use serde::Serialize;

pub const DISCOVERY_PORT: u16 = 47811;
const QUESTION: &[u8] = b"SALONERA?";
const ANSWER_PREFIX: &str = "SALONERA!";

#[derive(Serialize, Clone, Debug, PartialEq)]
#[serde(rename_all = "camelCase")]
pub struct FoundServer {
    pub address: String,
    pub port: u16,
    pub business_name: String,
    pub machine_name: String,
}

/// Ana bilgisayarda çalışır: soru gelince cevap verir. Ayrı bir iş parçacığında sonsuza kadar döner.
pub fn spawn_responder(server_port: u16, business_name: impl Fn() -> String + Send + 'static) -> std::io::Result<()> {
    let socket = UdpSocket::bind(("0.0.0.0", DISCOVERY_PORT))?;
    let machine = machine_name();
    std::thread::Builder::new().name("salonera-kesif".into()).spawn(move || {
        let mut buffer = [0u8; 64];
        loop {
            let Ok((size, from)) = socket.recv_from(&mut buffer) else { continue };
            if &buffer[..size] == QUESTION {
                let answer = format!("{ANSWER_PREFIX}{server_port}|{}|{machine}", business_name().replace('|', " "));
                let _ = socket.send_to(answer.as_bytes(), from);
            }
        }
    })?;
    Ok(())
}

/// Diğer bilgisayarlarda çalışır: ağa sorar ve `wait` süresince gelen cevapları toplar.
pub fn find_servers(wait: Duration) -> std::io::Result<Vec<FoundServer>> {
    let socket = UdpSocket::bind(("0.0.0.0", 0))?;
    socket.set_broadcast(true)?;
    socket.set_read_timeout(Some(Duration::from_millis(200)))?;
    socket.send_to(QUESTION, ("255.255.255.255", DISCOVERY_PORT))?;
    // Aynı bilgisayarda çalışan ana sunucuyu da bul (bazı ağlarda yayın geri dönmez).
    let _ = socket.send_to(QUESTION, ("127.0.0.1", DISCOVERY_PORT));

    let deadline = Instant::now() + wait;
    let mut found: Vec<FoundServer> = Vec::new();
    let mut buffer = [0u8; 512];
    while Instant::now() < deadline {
        if let Ok((size, from)) = socket.recv_from(&mut buffer) {
            if let Some(server) = parse_answer(&buffer[..size], from) {
                if !found.iter().any(|x| x.machine_name == server.machine_name && x.port == server.port) {
                    found.push(server);
                }
            }
        }
    }
    Ok(found)
}

fn parse_answer(data: &[u8], from: SocketAddr) -> Option<FoundServer> {
    let text = std::str::from_utf8(data).ok()?.strip_prefix(ANSWER_PREFIX)?;
    let mut parts = text.splitn(3, '|');
    let port = parts.next()?.parse().ok()?;
    let business_name = parts.next()?.to_string();
    let machine_name = parts.next().unwrap_or_default().to_string();
    Some(FoundServer { address: from.ip().to_string(), port, business_name, machine_name })
}

pub fn machine_name() -> String {
    std::env::var("COMPUTERNAME").or_else(|_| std::env::var("HOSTNAME")).unwrap_or_else(|_| "Ana bilgisayar".into())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parses_answer() {
        let from: SocketAddr = "192.168.1.20:47811".parse().unwrap();
        let server = parse_answer(b"SALONERA!47800|Kristal Dugun Salonu|OFIS-PC", from).unwrap();
        assert_eq!(server.address, "192.168.1.20");
        assert_eq!(server.port, 47800);
        assert_eq!(server.business_name, "Kristal Dugun Salonu");
        assert_eq!(server.machine_name, "OFIS-PC");
    }
}
