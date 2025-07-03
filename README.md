
# ☁️ Nube Personal API

API en ASP.NET Core para subir, listar y descargar archivos desde cualquier dispositivo a tu PC.

Este es un **proyecto personal y de aprendizaje**. No está pensado para producción.

---

## ✨ Funcionalidades

- Subida de archivos a raíz o carpetas
- Creación y eliminación de carpetas
- Listado de archivos (raíz, carpeta, todos)
- Descarga de carpetas como `.zip`
- Eliminación de archivos
- Autenticación JWT
- Swagger UI en `/docs`

---

## ⚙️ Requisitos

- .NET 8 SDK
- Archivo `appsettings.json` con configuración JWT y credenciales:

```Ejemplo json
{
  "JwtSettings": {
    "SecretKey": "clave-super-secreta",
    "Issuer": "tu-app",
    "Audience": "usuarios"
  },
  "AdminCredentials": {
    "Username": "admin",
    "Password": "admin123"
  }
}
```

---

## ▶️ Ejecutar

```bash
dotnet run
```

Luego accedé a [http://localhost:5000/docs](http://localhost:5000/docs) para ver Swagger.

---

## 🔐 Login

Usá `/api/auth/login` para obtener un token JWT y usarlo en los endpoints protegidos.

---

## 🗂️ Notas

- Los archivos se guardan en `wwwroot/Archivos/` (ignorado en git).
- Este proyecto es solo para uso personal/local.

---

## 📚 Licencia

MIT - Hecho por [maurocosentino]
