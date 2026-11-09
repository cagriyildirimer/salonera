use axum::http::StatusCode;
use axum::response::{IntoResponse, Response};
use axum::Json;
use serde::Serialize;

/// Uygulama hataları. `Rule` mesajları kullanıcıya olduğu gibi gösterilir; bu yüzden
/// her zaman sade Türkçe ve ne yapılacağını söyleyen cümleler olmalıdır.
#[derive(Debug, thiserror::Error)]
pub enum AppError {
    #[error("{0}")]
    Rule(String),
    #[error("Bu işlem için yetkiniz yok.")]
    Forbidden,
    #[error("{0}")]
    NotFound(String),
    #[error("Oturumunuz kapanmış. Lütfen tekrar giriş yapın.")]
    Unauthorized,
    #[error("Beklenmeyen bir sorun oluştu: {0}")]
    Internal(String),
}

pub type AppResult<T> = Result<T, AppError>;

pub fn rule<T>(message: impl Into<String>) -> AppResult<T> {
    Err(AppError::Rule(message.into()))
}

impl From<rusqlite::Error> for AppError {
    fn from(error: rusqlite::Error) -> Self {
        match error {
            rusqlite::Error::QueryReturnedNoRows => AppError::NotFound("Kayıt bulunamadı.".into()),
            other => AppError::Internal(other.to_string()),
        }
    }
}

#[derive(Serialize)]
struct ErrorBody {
    message: String,
}

impl IntoResponse for AppError {
    fn into_response(self) -> Response {
        let status = match &self {
            AppError::Rule(_) => StatusCode::BAD_REQUEST,
            AppError::Forbidden => StatusCode::FORBIDDEN,
            AppError::NotFound(_) => StatusCode::NOT_FOUND,
            AppError::Unauthorized => StatusCode::UNAUTHORIZED,
            AppError::Internal(detail) => {
                tracing::error!("iç hata: {detail}");
                StatusCode::INTERNAL_SERVER_ERROR
            }
        };
        let message = match &self {
            AppError::Internal(_) => "Beklenmeyen bir sorun oluştu. Tekrar deneyin; sorun sürerse yöneticinize haber verin.".to_string(),
            other => other.to_string(),
        };
        (status, Json(ErrorBody { message })).into_response()
    }
}
