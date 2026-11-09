import { useEffect, useState } from "react";
import { Icon } from "../components/Icon";
import { Alert, errorText } from "../components/ui";
import { shell } from "../shell";
import type { FoundServer } from "../types";
import { AuthLayout } from "./Login";

/** İlk açılış: bu bilgisayar verileri mi tutacak, yoksa ana bilgisayara mı bağlanacak? */
export function FirstRun({ onDone }: { onDone: () => void }) {
  const [step, setStep] = useState<"choose" | "find">("choose");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  const becomeMain = async () => {
    setBusy(true);
    setError("");
    try {
      await shell.becomeMain();
      onDone();
    } catch (e) {
      setError(errorText(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <AuthLayout>
      {step === "choose" ? (
        <div className="auth-card stack">
          <div>
            <h2 className="serif" style={{ fontSize: 32, fontWeight: 400 }}>Hoş geldiniz</h2>
            <p className="muted" style={{ fontSize: 17, marginTop: 8 }}>
              Salonera'yı bu bilgisayarda ilk kez açıyorsunuz. Bu bilgisayar hangisi?
            </p>
          </div>
          <button className="choice" disabled={busy} onClick={becomeMain}>
            <div className="choice-icon"><Icon name="building" size={24} /></div>
            <div>
              <div className="choice-title">Ana bilgisayar</div>
              <div className="choice-sub">
                Tüm kayıtlar bu bilgisayarda saklanır. Salonda ilk kurulan bilgisayar budur; çalışma saatlerinde açık kalmalıdır.
              </div>
            </div>
            <Icon name="right" />
          </button>
          <button className="choice" disabled={busy} onClick={() => setStep("find")}>
            <div className="choice-icon"><Icon name="monitor" size={24} /></div>
            <div>
              <div className="choice-title">Diğer bilgisayar</div>
              <div className="choice-sub">Ana bilgisayar zaten kurulu. Bu bilgisayar ağ üzerinden ona bağlanır.</div>
            </div>
            <Icon name="right" />
          </button>
          {error && <Alert kind="danger">{error}</Alert>}
        </div>
      ) : (
        <FindServer onBack={() => setStep("choose")} onDone={onDone} />
      )}
    </AuthLayout>
  );
}

function FindServer({ onBack, onDone }: { onBack: () => void; onDone: () => void }) {
  const [servers, setServers] = useState<FoundServer[] | null>(null);
  const [manual, setManual] = useState(false);
  const [address, setAddress] = useState("");
  const [error, setError] = useState("");

  const search = async () => {
    setServers(null);
    setError("");
    try {
      setServers(await shell.findServers());
    } catch (e) {
      setServers([]);
      setError(errorText(e));
    }
  };

  useEffect(() => { void search(); }, []);

  const connect = async (url: string) => {
    try {
      await shell.connectTo(url);
      onDone();
    } catch (e) {
      setError(errorText(e));
    }
  };

  return (
    <div className="auth-card stack">
      <div>
        <h2 className="serif" style={{ fontSize: 30, fontWeight: 400 }}>Ana bilgisayarı bulalım</h2>
        <p className="muted" style={{ fontSize: 17, marginTop: 8 }}>
          Ana bilgisayarın açık olduğundan ve Salonera'nın orada çalıştığından emin olun.
        </p>
      </div>
      {servers === null ? (
        <div className="row" style={{ padding: 18 }}><div className="spinner" /><span className="muted">Ağda aranıyor…</span></div>
      ) : servers.length === 0 ? (
        <Alert kind="warning">
          Ağda Salonera ana bilgisayarı bulunamadı. Ana bilgisayarın açık olduğunu ve iki bilgisayarın aynı modeme bağlı olduğunu
          kontrol edip tekrar arayın.
        </Alert>
      ) : (
        <div className="stack" style={{ gap: 10 }}>
          {servers.map((s) => (
            <button key={`${s.address}:${s.port}`} className="choice" onClick={() => connect(`http://${s.address}:${s.port}`)}>
              <div className="choice-icon"><Icon name="building" size={24} /></div>
              <div>
                <div className="choice-title">{s.businessName || "Salonera"}</div>
                <div className="choice-sub">{s.machineName} · {s.address}</div>
              </div>
              <span className="btn btn-primary" style={{ marginLeft: "auto" }}>Bağlan</span>
            </button>
          ))}
        </div>
      )}
      {error && <Alert kind="danger">{error}</Alert>}
      {manual ? (
        <div className="field">
          <label>Ana bilgisayarın adresi</label>
          <div className="row" style={{ flexWrap: "nowrap" }}>
            <input className="input" placeholder="192.168.1.20" value={address} onChange={(e) => setAddress(e.target.value)} />
            <button className="btn btn-primary" disabled={!address.trim()}
              onClick={() => connect(`http://${address.trim().replace(/^https?:\/\//, "").replace(/:\d+$/, "")}:47800`)}>
              Bağlan
            </button>
          </div>
          <span className="hint">Adresi ana bilgisayardaki Salonera'da Ayarlar → Bağlantı bölümünde görebilirsiniz.</span>
        </div>
      ) : null}
      <div className="row">
        <button className="btn" onClick={onBack}><Icon name="back" size={18} /> Geri</button>
        <span className="spacer" />
        {!manual && <button className="btn btn-ghost" onClick={() => setManual(true)}>Adresi kendim yazacağım</button>}
        <button className="btn" onClick={search}><Icon name="refresh" size={18} /> Tekrar ara</button>
      </div>
    </div>
  );
}
