use std::net::SocketAddr;
use std::sync::Arc;

use axum::extract::{FromRequestParts, Path, Query, State};
use axum::http::request::Parts;
use axum::http::header::AUTHORIZATION;
use axum::routing::{get, post, put};
use axum::{Json, Router};
use chrono::{Datelike, NaiveDate};
use serde::Deserialize;
use tower_http::cors::CorsLayer;

use crate::auth::{self, SessionUser};
use crate::catalog::{self, ProductTable};
use crate::error::{AppError, AppResult};
use crate::model::*;
use crate::{backup, customers, payments, program, reports, reservations, App, Ctx};

type AppState = Arc<App>;

/// Giriş yapmış kullanıcı. `Authorization: Bearer <anahtar>` başlığından okunur.
pub struct Auth(pub SessionUser);

impl FromRequestParts<AppState> for Auth {
    type Rejection = AppError;

    async fn from_request_parts(parts: &mut Parts, app: &AppState) -> Result<Self, Self::Rejection> {
        let token = parts
            .headers
            .get(AUTHORIZATION)
            .and_then(|v| v.to_str().ok())
            .and_then(|v| v.strip_prefix("Bearer "))
            .ok_or(AppError::Unauthorized)?;
        app.sessions.find(token).map(Auth).ok_or(AppError::Unauthorized)
    }
}

/// Veritabanı işini ayrı bir iş parçacığında çalıştırır; ağ sunucusu bu sırada beklemez.
async fn run<T: Send + 'static>(
    app: &AppState,
    work: impl FnOnce(&App) -> AppResult<T> + Send + 'static,
) -> AppResult<Json<T>> {
    let app = app.clone();
    tokio::task::spawn_blocking(move || work(&app))
        .await
        .map_err(|e| AppError::Internal(e.to_string()))?
        .map(Json)
}

async fn as_user<T: Send + 'static>(
    app: &AppState,
    user: SessionUser,
    work: impl FnOnce(&mut rusqlite::Connection, &Ctx) -> AppResult<T> + Send + 'static,
) -> AppResult<Json<T>> {
    run(app, move |app| {
        let ctx = app.ctx(user);
        app.with_db(|conn| work(conn, &ctx))
    })
    .await
}

pub fn router(app: AppState) -> Router {
    Router::new()
        .route("/api/info", get(info))
        .route("/api/setup", post(setup))
        .route("/api/login-users", get(login_users))
        .route("/api/login", post(login))
        .route("/api/logout", post(logout))
        .route("/api/me", get(me))
        .route("/api/me/password", post(change_password))
        .route("/api/settings", get(get_settings).put(update_settings))
        .route("/api/halls", get(list_halls).post(create_hall))
        .route("/api/halls/{id}", put(update_hall))
        .route("/api/packages", get(list_packages).post(create_package))
        .route("/api/packages/{id}", put(update_package))
        .route("/api/extras", get(list_extras).post(create_extra))
        .route("/api/extras/{id}", put(update_extra))
        .route("/api/users", get(list_users).post(create_user))
        .route("/api/users/{id}", put(update_user))
        .route("/api/customers", get(list_customers).post(create_customer))
        .route("/api/customers/{id}", get(customer_detail).put(update_customer).delete(delete_customer))
        .route("/api/calendar", get(calendar))
        .route("/api/availability", get(availability))
        .route("/api/reservations", get(search_reservations).post(create_reservation))
        .route("/api/reservations/{id}", get(reservation_detail).put(update_reservation))
        .route("/api/reservations/{id}/confirm", post(confirm_reservation))
        .route("/api/reservations/{id}/extend", post(extend_option))
        .route("/api/reservations/{id}/cancel", post(cancel_reservation))
        .route("/api/reservations/{id}/plan", put(set_plan))
        .route("/api/reservations/{id}/payments", post(take_payment))
        .route("/api/reservations/{id}/refunds", post(refund))
        .route("/api/reservations/{id}/program", get(get_program).put(save_program))
        .route("/api/payments", get(list_payments))
        .route("/api/payments/{id}", get(get_payment))
        .route("/api/payments/{id}/cancel", post(cancel_payment))
        .route("/api/dashboard", get(dashboard))
        .route("/api/reports/year", get(year_report))
        .route("/api/reports/receivables", get(receivables))
        .route("/api/reports/halls", get(hall_usage))
        .route("/api/backups", get(list_backups).post(create_backup))
        .layer(CorsLayer::permissive())
        .with_state(app)
}

/// Sunucuyu başlatır ve kapanana kadar çalışır. Saatte bir, o gün yedek yoksa yedek alır.
pub async fn serve(app: AppState, port: u16) -> std::io::Result<()> {
    let listener = tokio::net::TcpListener::bind(SocketAddr::from(([0, 0, 0, 0], port))).await?;
    let backup_app = app.clone();
    tokio::spawn(async move {
        let mut tick = tokio::time::interval(std::time::Duration::from_secs(3600));
        loop {
            tick.tick().await;
            let app = backup_app.clone();
            let _ = tokio::task::spawn_blocking(move || daily_backup(&app)).await;
        }
    });
    axum::serve(listener, router(app)).await
}

fn daily_backup(app: &App) {
    let Some(dir) = &app.backup_dir else { return };
    let now = app.now();
    if backup::has_backup_for(dir, now) {
        return;
    }
    let ready = app.with_db(|conn| auth::is_set_up(conn)).unwrap_or(false);
    if ready {
        if let Err(error) = app.with_db(|conn| backup::create(conn, dir, now)) {
            tracing::error!("otomatik yedek alınamadı: {error}");
        }
    }
}

// ---------------- Kurulum ve giriş ----------------
async fn info(State(app): State<AppState>) -> AppResult<Json<ServerInfo>> {
    run(&app, |app| app.with_db(|c| auth::server_info(c))).await
}

async fn setup(State(app): State<AppState>, Json(body): Json<SetupRequest>) -> AppResult<Json<()>> {
    run(&app, move |app| {
        let now = app.now();
        app.with_db(|c| auth::setup(c, &body, now))
    })
    .await
}

async fn login_users(State(app): State<AppState>) -> AppResult<Json<Vec<LoginUserOption>>> {
    run(&app, |app| app.with_db(|c| auth::login_users(c))).await
}

async fn login(State(app): State<AppState>, Json(body): Json<LoginRequest>) -> AppResult<Json<LoginResponse>> {
    run(&app, move |app| {
        let now = app.now();
        let user = app.with_db(|c| auth::login(c, &body, now))?;
        let dto = app.with_db(|c| auth::user_dto(c, &user.id))?;
        Ok(LoginResponse { token: app.sessions.create(user), user: dto })
    })
    .await
}

async fn logout(State(app): State<AppState>, parts: axum::http::HeaderMap) -> AppResult<Json<()>> {
    if let Some(token) = parts.get(AUTHORIZATION).and_then(|v| v.to_str().ok()).and_then(|v| v.strip_prefix("Bearer ")) {
        app.sessions.remove(token);
    }
    Ok(Json(()))
}

async fn me(State(app): State<AppState>, Auth(user): Auth) -> AppResult<Json<UserDto>> {
    as_user(&app, user, |c, ctx| auth::user_dto(c, &ctx.user.id)).await
}

async fn change_password(
    State(app): State<AppState>,
    Auth(user): Auth,
    Json(body): Json<ChangePasswordRequest>,
) -> AppResult<Json<()>> {
    as_user(&app, user, move |c, ctx| auth::change_password(c, ctx, &body)).await
}

// ---------------- Ayarlar ----------------
#[derive(Deserialize)]
struct AllQuery {
    #[serde(default)]
    all: bool,
}

async fn get_settings(State(app): State<AppState>, Auth(user): Auth) -> AppResult<Json<BusinessSettings>> {
    as_user(&app, user, |c, _| catalog::get_settings(c)).await
}

async fn update_settings(
    State(app): State<AppState>,
    Auth(user): Auth,
    Json(body): Json<BusinessSettings>,
) -> AppResult<Json<BusinessSettings>> {
    as_user(&app, user, move |c, ctx| catalog::update_settings(c, ctx, &body)).await
}

async fn list_halls(State(app): State<AppState>, Auth(user): Auth, Query(q): Query<AllQuery>) -> AppResult<Json<Vec<HallDto>>> {
    as_user(&app, user, move |c, _| catalog::list_halls(c, q.all)).await
}

async fn create_hall(State(app): State<AppState>, Auth(user): Auth, Json(body): Json<SaveHallRequest>) -> AppResult<Json<String>> {
    as_user(&app, user, move |c, ctx| catalog::save_hall(c, ctx, None, &body)).await
}

async fn update_hall(
    State(app): State<AppState>,
    Auth(user): Auth,
    Path(id): Path<String>,
    Json(body): Json<SaveHallRequest>,
) -> AppResult<Json<String>> {
    as_user(&app, user, move |c, ctx| catalog::save_hall(c, ctx, Some(&id), &body)).await
}

async fn list_packages(State(app): State<AppState>, Auth(user): Auth, Query(q): Query<AllQuery>) -> AppResult<Json<Vec<ProductDto>>> {
    as_user(&app, user, move |c, _| catalog::list_products(c, ProductTable::Packages, q.all)).await
}

async fn create_package(State(app): State<AppState>, Auth(user): Auth, Json(body): Json<SaveProductRequest>) -> AppResult<Json<String>> {
    as_user(&app, user, move |c, ctx| catalog::save_product(c, ctx, ProductTable::Packages, None, &body)).await
}

async fn update_package(
    State(app): State<AppState>,
    Auth(user): Auth,
    Path(id): Path<String>,
    Json(body): Json<SaveProductRequest>,
) -> AppResult<Json<String>> {
    as_user(&app, user, move |c, ctx| catalog::save_product(c, ctx, ProductTable::Packages, Some(&id), &body)).await
}

async fn list_extras(State(app): State<AppState>, Auth(user): Auth, Query(q): Query<AllQuery>) -> AppResult<Json<Vec<ProductDto>>> {
    as_user(&app, user, move |c, _| catalog::list_products(c, ProductTable::Extras, q.all)).await
}

async fn create_extra(State(app): State<AppState>, Auth(user): Auth, Json(body): Json<SaveProductRequest>) -> AppResult<Json<String>> {
    as_user(&app, user, move |c, ctx| catalog::save_product(c, ctx, ProductTable::Extras, None, &body)).await
}

async fn update_extra(
    State(app): State<AppState>,
    Auth(user): Auth,
    Path(id): Path<String>,
    Json(body): Json<SaveProductRequest>,
) -> AppResult<Json<String>> {
    as_user(&app, user, move |c, ctx| catalog::save_product(c, ctx, ProductTable::Extras, Some(&id), &body)).await
}

async fn list_users(State(app): State<AppState>, Auth(user): Auth) -> AppResult<Json<Vec<UserDto>>> {
    as_user(&app, user, |c, ctx| auth::list_users(c, ctx)).await
}

async fn create_user(State(app): State<AppState>, Auth(user): Auth, Json(body): Json<SaveUserRequest>) -> AppResult<Json<UserDto>> {
    as_user(&app, user, move |c, ctx| auth::create_user(c, ctx, &body)).await
}

async fn update_user(
    State(app): State<AppState>,
    Auth(user): Auth,
    Path(id): Path<String>,
    Json(body): Json<SaveUserRequest>,
) -> AppResult<Json<UserDto>> {
    let sessions_app = app.clone();
    let result = as_user(&app, user, move |c, ctx| auth::update_user(c, ctx, &id, &body)).await?;
    // Pasif yapılan kullanıcının oturumu hemen kapanır; diğerlerinin rol/ad bilgisi güncellenir.
    if result.0.is_active {
        sessions_app.sessions.refresh_user(&SessionUser {
            id: result.0.id.clone(),
            name: result.0.name.clone(),
            role: result.0.role,
        });
    } else {
        sessions_app.sessions.remove_user(&result.0.id);
    }
    Ok(result)
}

// ---------------- Müşteriler ----------------
#[derive(Deserialize)]
struct SearchQuery {
    q: Option<String>,
}

async fn list_customers(State(app): State<AppState>, Auth(user): Auth, Query(q): Query<SearchQuery>) -> AppResult<Json<Vec<CustomerDto>>> {
    as_user(&app, user, move |c, _| customers::list(c, q.q.as_deref())).await
}

async fn create_customer(State(app): State<AppState>, Auth(user): Auth, Json(body): Json<SaveCustomerRequest>) -> AppResult<Json<CustomerDto>> {
    as_user(&app, user, move |c, ctx| {
        let id = customers::insert(c, ctx, &body)?;
        customers::get(c, &id)
    })
    .await
}

async fn customer_detail(State(app): State<AppState>, Auth(user): Auth, Path(id): Path<String>) -> AppResult<Json<CustomerDetail>> {
    as_user(&app, user, move |c, ctx| customers::detail(c, &id, ctx.today())).await
}

async fn update_customer(
    State(app): State<AppState>,
    Auth(user): Auth,
    Path(id): Path<String>,
    Json(body): Json<SaveCustomerRequest>,
) -> AppResult<Json<CustomerDto>> {
    as_user(&app, user, move |c, ctx| customers::update(c, ctx, &id, &body)).await
}

async fn delete_customer(State(app): State<AppState>, Auth(user): Auth, Path(id): Path<String>) -> AppResult<Json<()>> {
    as_user(&app, user, move |c, ctx| customers::delete(c, ctx, &id)).await
}

// ---------------- Rezervasyonlar ----------------
#[derive(Deserialize)]
struct RangeQuery {
    from: NaiveDate,
    to: NaiveDate,
}

#[derive(Deserialize)]
struct AvailabilityQuery {
    date: NaiveDate,
    start: String,
    end: String,
    exclude: Option<String>,
}

async fn calendar(State(app): State<AppState>, Auth(user): Auth, Query(q): Query<RangeQuery>) -> AppResult<Json<Vec<ReservationSummary>>> {
    if (q.to - q.from).num_days() > 400 {
        return Err(AppError::Rule("Tarih aralığı en fazla bir yıl olabilir.".into()));
    }
    as_user(&app, user, move |c, ctx| reservations::calendar(c, q.from, q.to, ctx.today())).await
}

async fn availability(
    State(app): State<AppState>,
    Auth(user): Auth,
    Query(q): Query<AvailabilityQuery>,
) -> AppResult<Json<Vec<HallAvailability>>> {
    as_user(&app, user, move |c, ctx| {
        reservations::availability(c, q.date, &q.start, &q.end, q.exclude.as_deref(), ctx.today())
    })
    .await
}

async fn search_reservations(
    State(app): State<AppState>,
    Auth(user): Auth,
    Query(q): Query<SearchQuery>,
) -> AppResult<Json<Vec<ReservationSummary>>> {
    as_user(&app, user, move |c, ctx| reservations::search(c, q.q.as_deref().unwrap_or(""), ctx.today())).await
}

async fn create_reservation(
    State(app): State<AppState>,
    Auth(user): Auth,
    Json(body): Json<CreateReservationRequest>,
) -> AppResult<Json<CreateReservationResponse>> {
    as_user(&app, user, move |c, ctx| reservations::create(c, ctx, &body)).await
}

async fn reservation_detail(State(app): State<AppState>, Auth(user): Auth, Path(id): Path<String>) -> AppResult<Json<ReservationDetail>> {
    as_user(&app, user, move |c, ctx| reservations::detail(c, &id, ctx.today())).await
}

async fn update_reservation(
    State(app): State<AppState>,
    Auth(user): Auth,
    Path(id): Path<String>,
    Json(body): Json<UpdateReservationRequest>,
) -> AppResult<Json<()>> {
    as_user(&app, user, move |c, ctx| reservations::update(c, ctx, &id, &body)).await
}

async fn confirm_reservation(
    State(app): State<AppState>,
    Auth(user): Auth,
    Path(id): Path<String>,
    Json(body): Json<SaleInput>,
) -> AppResult<Json<Option<String>>> {
    as_user(&app, user, move |c, ctx| reservations::confirm(c, ctx, &id, &body)).await
}

async fn extend_option(
    State(app): State<AppState>,
    Auth(user): Auth,
    Path(id): Path<String>,
    Json(body): Json<ExtendOptionRequest>,
) -> AppResult<Json<()>> {
    as_user(&app, user, move |c, ctx| reservations::extend_option(c, ctx, &id, body.days)).await
}

async fn cancel_reservation(
    State(app): State<AppState>,
    Auth(user): Auth,
    Path(id): Path<String>,
    Json(body): Json<ReasonRequest>,
) -> AppResult<Json<()>> {
    as_user(&app, user, move |c, ctx| reservations::cancel(c, ctx, &id, &body.reason)).await
}

async fn set_plan(
    State(app): State<AppState>,
    Auth(user): Auth,
    Path(id): Path<String>,
    Json(body): Json<SetPlanRequest>,
) -> AppResult<Json<()>> {
    as_user(&app, user, move |c, ctx| reservations::set_plan(c, ctx, &id, &body.plan)).await
}

// ---------------- Ödemeler ----------------
async fn take_payment(
    State(app): State<AppState>,
    Auth(user): Auth,
    Path(id): Path<String>,
    Json(body): Json<PaymentInput>,
) -> AppResult<Json<String>> {
    as_user(&app, user, move |c, ctx| payments::take(c, ctx, &id, &body)).await
}

async fn refund(
    State(app): State<AppState>,
    Auth(user): Auth,
    Path(id): Path<String>,
    Json(body): Json<RefundRequest>,
) -> AppResult<Json<String>> {
    as_user(&app, user, move |c, ctx| payments::refund(c, ctx, &id, &body)).await
}

async fn get_program(State(app): State<AppState>, Auth(user): Auth, Path(id): Path<String>) -> AppResult<Json<Program>> {
    as_user(&app, user, move |c, _| program::get(c, &id)).await
}

async fn save_program(
    State(app): State<AppState>,
    Auth(user): Auth,
    Path(id): Path<String>,
    Json(body): Json<Program>,
) -> AppResult<Json<Program>> {
    as_user(&app, user, move |c, ctx| program::save(c, ctx, &id, &body)).await
}

async fn list_payments(State(app): State<AppState>, Auth(user): Auth, Query(q): Query<RangeQuery>) -> AppResult<Json<Vec<PaymentDto>>> {
    as_user(&app, user, move |c, _| payments::list_between(c, q.from, q.to)).await
}

async fn get_payment(State(app): State<AppState>, Auth(user): Auth, Path(id): Path<String>) -> AppResult<Json<PaymentDto>> {
    as_user(&app, user, move |c, _| payments::get(c, &id)).await
}

async fn cancel_payment(
    State(app): State<AppState>,
    Auth(user): Auth,
    Path(id): Path<String>,
    Json(body): Json<ReasonRequest>,
) -> AppResult<Json<()>> {
    as_user(&app, user, move |c, ctx| payments::cancel(c, ctx, &id, &body.reason)).await
}

// ---------------- Ana sayfa, raporlar, yedek ----------------
#[derive(Deserialize)]
struct YearQuery {
    year: Option<i32>,
}

async fn dashboard(State(app): State<AppState>, Auth(user): Auth) -> AppResult<Json<Dashboard>> {
    as_user(&app, user, |c, ctx| reports::dashboard(c, ctx.today())).await
}

async fn year_report(State(app): State<AppState>, Auth(user): Auth, Query(q): Query<YearQuery>) -> AppResult<Json<YearReport>> {
    as_user(&app, user, move |c, ctx| reports::year(c, ctx, q.year.unwrap_or(ctx.now.year()))).await
}

async fn receivables(State(app): State<AppState>, Auth(user): Auth) -> AppResult<Json<Vec<ReservationSummary>>> {
    as_user(&app, user, |c, ctx| reports::receivables(c, ctx)).await
}

async fn hall_usage(State(app): State<AppState>, Auth(user): Auth, Query(q): Query<RangeQuery>) -> AppResult<Json<Vec<HallUsageRow>>> {
    as_user(&app, user, move |c, ctx| reports::hall_usage(c, ctx, q.from, q.to)).await
}

async fn list_backups(State(app): State<AppState>, Auth(user): Auth) -> AppResult<Json<Vec<BackupInfo>>> {
    user.require_admin()?;
    run(&app, |app| match &app.backup_dir {
        Some(dir) => backup::list(dir),
        None => Ok(Vec::new()),
    })
    .await
}

async fn create_backup(State(app): State<AppState>, Auth(user): Auth) -> AppResult<Json<BackupInfo>> {
    user.require_admin()?;
    run(&app, |app| {
        let dir = app.backup_dir.clone().ok_or_else(|| AppError::Rule("Yedek klasörü ayarlanmamış.".into()))?;
        let now = app.now();
        app.with_db(|c| backup::create(c, &dir, now))
    })
    .await
}
