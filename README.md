# ORKIN · Cotizador Web

> **README de trabajo** — documento vivo del proyecto. Aquí se registra el análisis de la fuente
> (Excel actual), la arquitectura objetivo, la estructura de solución, el plan de fases y el estado
> de cada tarea. **Este archivo se actualiza al cerrar cada tarea.**

| Campo | Valor |
|---|---|
| Cliente | ORKIN — Control de Plagas y Servicios de Fumigación (Nicaragua) |
| Producto | Aplicación web de cotización (y posterior suite de módulos operativos) |
| Módulo inicial | **Cotizador** (reemplaza el Excel `.xlsm` del día a día) |
| Stack | **.NET 10 (`net10.0`)** · Blazor WebAssembly · **MudBlazor 9** · ASP.NET Core Web API · EF Core 10 |
| Base de datos | **SQL Server** (confirmado) |
| Seguridad / autenticación | **Propia** — ASP.NET Core Identity + JWT. No depende de ningún otro proyecto |
| Referencia de estructura | `D:\repos\MerakodeSoft\Mikaela.Prestamos` (solo patrón técnico: solución, convenciones, DI, despliegue) |
| Diseño de UI aprobado | **`Cotizador Web — Prototipo D (sidebar + Cotizaciones).html`** |
| Aplicación móvil | **Fuera de alcance.** Si se hace, será un proyecto/repositorio separado |
| Especificación funcional | `Cotizador_Web_Documento_de_Arranque.docx` (Project Charter + SRS, v1.0) |
| Fuente de la verdad de negocio | `Cotizador Original - Agosto 31 2026.xlsm` (con las correcciones de §2.4 ya validadas) |
| Fecha de arranque | 2026-09-27 |
| Estado actual | **Fase 0 — modelando la base de datos** |

---

## 1. Fuentes analizadas

| Archivo | Qué aporta |
|---|---|
| `Cotizador Original - Agosto 31 2026.xlsm` | Herramienta actual. 5 hojas, 10 tablas de Excel, 2 macros VBA. Fuente de verdad del motor de cálculo. |
| `Cotizador_Web_Documento_de_Arranque.docx` | Especificación funcional y técnica (alcance, arquitectura, modelo de datos, fases). |
| `Cotizador Web — Prototipo D (sidebar + Cotizaciones).html` | **Diseño de UI aprobado.** Sidebar + panel de cotizaciones + overlay de pantalla completa. |
| `Cotizador Web - Prototipo C (pantalla única).html` | Alternativa: todo en un panel sin overlay. Descartada. |
| `Cotizador Web - Prototipo B (sidebar).html` | Alternativa: formularios "clásico" sin overlay. Descartada. |
| `Cotizador Web - Prototipo de pantallas.html` / `...Web2...html` | Wireframes iniciales multi-paso. Sirven de referencia para el desglose de pasos. |
| `D:\repos\MerakodeSoft\Mikaela.Prestamos` | Patrón de solución, convenciones de código, DI, auth, despliegue, CI/CD, estructura de `docs/`. |

### 1.1 Por qué el **`Cotizador Web — Prototipo D (sidebar + Cotizaciones).html`**

- Sidebar fijo en escritorio → **topbar de chips en móvil** (`@media max-width:760px`): patrón que funciona en campo.
- El formulario de cotización es un **overlay a pantalla completa** con header morado, barra inferior sticky con el total en vivo (`C$ 5,727.77 ≈ $ 156.42`) y botón `Enviar a aprobación`. Esto replica bien el "wizard" del Word sin obligar al usuario a navegar.
- La lista de cotizaciones incluye **versión (`v1`), estado con badge y acciones contextuales** (`Editar` / `Anular` / `Nueva versión`) — ya refleja el versionado y el estado-anulado que pide el SRS.
- Incluye el grupo **"Próximamente"** en el sidebar (Calendario de trabajos, Plantillas de actividades, Catálogos) → señal visual de que el proyecto crece por módulos.
- Paleta: `--primary:#594AE2`, panel `#20263A`/`#171C2B`, fondo `#F4F5FA`. Se traslada directamente al tema MudBlazor.

**Ausencia a corregir:** ningún prototipo modela **login**. Se incluye explícitamente en el alcance (ver §6, Fase 1).

---

## 2. Análisis del Excel actual (fuente de verdad)

### 2.1 Hojas y tablas

| Hoja | Función | Tablas de Excel | Volumen |
|---|---|---|---|
| `Tabla de costo residencia` | Cotización residencial | `Tabla1` (A5:E108) insumos · `Tabla475` (A111:E130) amortizables | 103 insumos · 19 activos |
| `Tabla de costo empresa` | Cotización empresarial (por semestre) | `Tabla16` (A11:E89) 1er sem. · `Tabla168` (G11:K89) 2do sem. · `Tabla47` (A94:E107) amortizables | 78 · 78 · 13 |
| `Tabla productos` | Catálogo maestro | `Tabla1912` (F2:H166) código→descripción→costo · `Tabla10` (A2:D115) código/unidad/producto→precio · `Tabla9` (A117:C136) activos código→nombre→precio | 164 · 113 · 19 |
| `Tipo de cambio` | Histórico córdoba/dólar | `Tabla2` (C1:D1644) fecha→valor | 1 643 registros (2020-01-01 → 2024-06-30) |
| `Tipos de servicios` *(oculta)* | Catálogo de tipos de servicio | `TablaServicios` (B2:B14) | 11 tipos |

**11 tipos de servicio:** Sanitización · Control general · Tratamiento termitas · Control de cucarachas · Control mosquitos y moscas · Control de roedores · Control garrapatas · Control murciélagos · Control zompopos · Control Hormigas · Tratamiento fosfina.

**Macros VBA:** `Limpiar_Residencias` y `Limpiar_Empresas` (4 módulos). Reinician las cantidades a cero para reutilizar la misma hoja → **confirma que hoy no existe historial**.

### 2.2 Fórmulas literales (replicar exactamente)

**Residencial** — `Tabla de costo residencia`

```excel
H4   =TODAY()

Tabla1   (A5:E108)  Unidad de medida | Producto | Precio | Cantidad a utilizar | Total
  C6   =VLOOKUP([@Producto],Tabla10[[PRODUCTO]:[PRECIO]],2,FALSE)
  E6   =[@Precio]*[@[Cantidad a utilizar]]

Tabla475 (A111:E130) Producto | Precio unitario | Cantidad | Meses | Valor mensual
  B112 =VLOOKUP([@Producto],Tabla9[[#All],[Columna1]:[Columna2]],2,FALSE)
  E112 =IFERROR((B112*C112)/D112,"")

G6  "Costo de productos"        =SUM(Tabla1[Total])+SUM(Tabla475[Valor mensual])
H6  "Costo servicio con margen $"=(SUM(E6:E108)+SUM(Tabla475[Valor mensual]))
                                   +((SUM(Tabla1[Total])+SUM(Tabla475[Valor mensual]))*J6)
I6  "Costo servicio $"           =(H6)/(VLOOKUP($H$4,Tabla2[#All],2))
J6  "Margen deseado"             = 5.5        <-- formato "0%"  ⇒ representa 550 %
K6  "Porcentaje de plantilla"    =IFERROR(G6/H6,"")
H12 "Tipo de servicio"           = "Tratamiento termitas"   <-- texto informative, NO funcional
```

**Empresarial (por semestre)** — `Tabla de costo empresa`

```excel
D6   =TODAY()

Tabla16  (A11:E89)  1er Semestre: Unidad | Producto | Precio | Cantidad a utilizar | Total
  C12  =VLOOKUP([@Producto],Tabla10[[PRODUCTO]:[PRECIO]],2,FALSE)
  E12  =[@Precio]*[@[Cantidad a utilizar]]

Tabla168 (G11:K89)  2do Semestre: Unidad | Producto | Precio | Cantidad a utilizar | Total
  I12  =VLOOKUP(Tabla16[@Producto],Tabla10[[PRODUCTO]:[PRECIO]],2,FALSE)  ⚠ usa el Producto del 1er semestre
  K12  =Tabla16[@Precio]*Tabla16[@[Cantidad a utilizar]]                     ⚠ ignora J12

Tabla47  (A94:E107) Producto | Precio unitario | Cantidad | Meses | Valor mensual
  E95   =IFERROR((B95*C95)/D95,"")     ⚠ D (Meses) = 0 en las 13 filas ⇒ siempre ""

C8  "Costo sin margen"  =AVERAGE(SUM(Tabla16[Total]),SUM(Tabla168[Total]))+SUM(Tabla47[Valor mensual])
D8  "Precio (C$)"       =(C8)+(C8*F8)
E8  "Precio (US$)"      =(D8)/(VLOOKUP($D$6,Tabla2[#All],2))
F8  "Margen deseado"    = 8          <-- formato "0%"  ⇒ representa 800 %
G8  "Porcentaje de plantilla" =IFERROR(C8/D8,"")
```

### 2.3 Reglas de negocio a preservar (contrato del motor de cálculo)

| # | Regla | Origen | Nota |
|---|---|---|---|
| RN-01 | `TotalLínea = PrecioUnitario × Cantidad` | `Tabla1[E]`, `Tabla16[E]` | Cantidad admite decimales (ml, gr, H.H, km). |
| RN-02 | `ValorMensualActivo = (PrecioUnitario × Cantidad) / MesesVidaÚtil` | `Tabla475[E]`, `Tabla47[E]` | `Meses = 0` ⇒ el `IFERROR` devuelve `""` y `SUM` lo ignora (aporta 0). |
| RN-03 | `CostoProductos = Σ TotalLínea` | `SUM(Tabla1[Total])` | |
| RN-04 | `CostoTotal = CostoProductos + Σ ValorMensualActivo` | `G6` / `C8` | En residencial la etiqueta "Costo de productos" **incluye** amortizables: renombrar en el nuevo sistema. |
| RN-05 | `PrecioC$ = CostoTotal + (CostoTotal × Margen)` | `H6` / `D8` | `Margen` es una **fracción** (0.055 = 5.5 %). |
| RN-06 | `PrecioUS$ = PrecioC$ ÷ TipoCambio(Fecha)` | `I6` / `E8` | Ver **RN-09** para la regla de búsqueda del tipo de cambio. |
| RN-07 | Empresarial: `CostoTotal = PROMEDIO(Σ Total 1er sem, Σ Total 2do sem) + Σ ValorMensualActivo` | `C8` | El promedio es de los **dos semestres**; los amortizables se suman **una sola vez** fuera del promedio. |
| RN-08 | `PorcentajePlantilla = CostoTotal / PrecioC$` | `K6` / `G8` | Indicador de control interno, no interviene en el precio. |
| RN-09 | **Tipo de cambio:** el Excel usa `VLOOKUP(HOY(), Tabla2[#All], 2)` con **coincidencia exacta**. | `I6` / `E8` | En la web se usará *último valor conocido ≤ fecha de la cotización* (ver **H-01**). |
| RN-10 | El **margen no se deriva** del tipo de servicio (H12 es texto). El usuario lo escribe. | `H12` | En la web se propondrá un margen sugerido por tipo de servicio, editable. |

> ⚠️ **Nota de alcance (decisión del cliente, 2026-09-27):** el Excel es la fuente de los **datos** (catálogos, precios, tipos de servicio), pero **no** de la lógica cuando esta sea incorrecta. Todo lo listado en §2.4 se corrige en el sistema, y **cada corrección requiere validación del cliente antes de implementarse**. El detalle de cada corrección propuesta está en §2.4 y se irá marcando conforme se apruebe.

### 2.4 Hallazgos y riesgos detectados en el Excel — **a corregir todos, con validación previa**

| Id | Severidad | Hallazgo | Decisión propuesta |
|---|---|---|---|
| **H-01** | 🔴 Crítica | `I6` y `E8` devuelven **`#N/A` hoy**. `Tabla2` termina el **2024-06-30** y el `VLOOKUP` es de coincidencia **exacta** contra `=TODAY()`. La columna USD está rota desde hace más de un año. | Buscar *último TC ≤ fecha* con fallback configurable. Cargar TC histórico completo + job diario. |
| **H-02** | 🔴 Crítica | `J6 = 5.5` y `F8 = 8` con formato `0%` ⇒ **550 % y 800 %**. Solo funciona si el usuario teclea `5,5%`. Si abre la hoja sin editar margen, el precio se multiplica 6.5× / 9×. | Guardar el margen como `decimal(5,4)` (0.0550) con validación de rango 0–1 y regla de negocio explícita. Mostrar siempre el margen como porcentaje legible. |
| **H-03** | 🔴 Crítica | En `Tabla168` (2do semestre) `I12` busca por `Tabla16[@Producto]` y `K12` multiplica `Tabla16[@Precio]*Tabla16[@Cantidad]`. **El 2do semestre es una copia del 1ro**; las columnas H y J son decorativas. | **Requiere decisión del cliente**: ¿el 2do semestre debe cotizarse por separado (independiente) o se conserva la lógica actual (2do semestre = 1er)? El SRS (docx §2.2) asume independiente. |
| **H-04** | 🟠 Alta | `Tabla47` (amortizables de empresa) tiene `Meses = 0` en las 13 filas ⇒ los amortizables **aportan 0** a la cotización empresarial. | Pedir las vidas útiles correctas al cliente y hacerlas **obligatorias** (bloquear cantidad > 0 sin vida útil). |
| **H-05** | 🟠 Alta | Varias filas de `Tabla10` no tienen fórmula en la columna `PRECIO` (filas 14, 21, 85…) ⇒ `VLOOKUP` fallaría. Riesgo de cotizar con `#N/A`. | Validación: toda línea de cotización debe resolver precio; si no, **bloquear el guardado**. |
| **H-06** | 🟡 Media | `Tabla9` tiene encabezados sin nombre (`Columna1`, `Columna2`) y el `VLOOKUP` de amortizables busca **por nombre**, no por código ⇒ frágil ante acentos/mayúsculas. | Modelar activos con FK explícita a catálogo + `VidaUtilMeses` obligatoria. |
| **H-07** | 🟡 Media | El precio se toma de `Tabla10` (nombre conocido) que a su vez consulta `Tabla1912` (costo real) por código. **No hay columna de costo vs precio** en la hoja de cálculo ⇒ no se puede medir margen real por producto. | Modelar `Costo` y `Precio` por separado en `Producto`, con historial de cambios (auditoría). |
| **H-08** | 🟡 Media | `H6` repite el mismo `SUM` dos veces dentro de la misma fórmula (redundante). | Fórmula única y documentada en el motor de dominio. |
| **H-09** | 🟡 Media | Los precios se escriben a mano en `Tabla1912`; no hay vigencia ni historial. Un precio viejo sobrevive indefinidamente. | `Producto.PrecioDesde/PrecioHasta` + `PrecioHistorial` auditado. |
| **H-10** | 🟡 Media | Duplicados de producto con distinto nombre entre catálogos (p. ej. `Deltametrina` vs `DELTAMETRINA 2.5 (1 Lt)`, `Plástico ROLLO`/`Plástico YARDA`, `Omitox (100gr)/(250gr)/(500gr)`). | Consolidar a **un solo catálogo** con `Codigo` como clave natural y `Activo` para baja lógica. Decisión de limpieza en Fase 2. |
| **H-11** | 🟢 Baja | Sin cliente, sin dirección, sin contacto, sin historial → nada que vincule cotizaciones. | Módulo **Clientes**. |
| **H-12** | 🟢 Baja | Cero control de versiones, cero auditoría, cero aprobación, un solo usuario a la vez. | Ver §6. |

---

## 3. Decisiones del proyecto

### 3.1 ✅ Decisiones ya confirmadas (2026-09-27)

| Id | Decisión | Impacto |
|---|---|---|
| **D-01** | **Base de datos: SQL Server.** No PostgreSQL. | Cambia el provider de EF Core a `Microsoft.EntityFrameworkCore.SqlServer`, la convención de nombres de tablas (PascalCase + esquemas) y el diseño físico. Ver `docs/BASE-DE-DATOS.md`. |
| **D-02** | **Seguridad propia.** No se depende de `MerakodeSoft.Auth` ni de ningún otro proyecto. | `ASP.NET Core Identity` + `JwtBearer` propios. Tablas de usuarios/roles/permisos en el esquema `seg`. Sin BFF atado a un servicio externo. |
| **D-13** | **Sin proyecto MAUI.** Fuera de alcance. Si se hace, será un proyecto/repositorio separado. | Se elimina la carpeta `Orkin.Cotizador.UI/Orkin.Cotizador.UI` del plan. La Fase 10 queda solo como optimización responsive web. |
| **D-14** | **Se corrige todo lo que esté mal en el Excel** (los 12 hallazgos de §2.4), **con validación del cliente antes de implementar.** | Cada corrección es una tarea propia en §6 con su regla nueva y su caso de prueba. Nada se replica "por faithfulness" si está mal. |
| **D-18** | **.NET 10** en los 8 proyectos + **MudBlazor 9** para la UI. | Versiones fijadas en §4.1.1. Cero paquetes MAUI. |
| **ADR-001** | Diseño de UI = **`Cotizador Web — Prototipo D (sidebar + Cotizaciones).html`** | Ese archivo es la referencia visual. |
| **ADR-018** | **En el código no hay catálogos.** Los conjuntos de negocio viven en la base (`dbo.*Catalogo`), se editan desde Administración y se resuelven por `Codigo`. Ver §9. |
| **ADR-019** | **El formato del consecutivo es configurable, no código.** `Plantilla` con secciones `{PRE}{ANIO}{NUM}{SUF}` validada por UDF + `CHECK`. Ver §5.15/§6.13 de `BASE-DE-DATOS.md`. |

### 3.2 🔴 Decisiones aún abiertas (bloquean ciertas tareas)

| Id | Pregunta | Recomendación | Estado |
|---|---|---|---|
| **D-03** | **H-03**: ¿el 2do semestre empresarial se cotiza **independiente** del 1ro? | **Sí, independiente** (como dice el SRS). Requiere corregir `Tabla168`, que hoy copia el 1er semestre. | 🔴 Pendiente cliente |
| **D-04** | **H-02 / H-04**: ¿cuál es el **margen real** por tipo de servicio y la **vida útil real** de cada uno de los 19 activos amortizables? | Pedir tabla de márgenes (RN-10) y vida útil por activo. Sin esto no se pueden fijar los valores por defecto. | 🔴 Pendiente cliente |
| **D-05** | Reglas de aprobación: ¿**montos** y/o **márgenes** que disparan aprobación obligatoria? El docx sugiere "margen < 5 %" y "monto > umbral". | Parametrizable por rol, con valores por defecto configurables desde Administración. | 🔴 Pendiente cliente |
| **D-06** | Tipo de cambio: ¿**API del Banco Central de Nicaragua**? ¿Hay endpoint público? Si no, ¿de dónde se saca el TC diario? | Confirmar endpoint; si no hay, carga manual + job con archivo. | 🟡 Pendiente cliente |
| **D-07** | Proveedor de **correo saliente**: SMTP corporativo, SendGrid o Microsoft Graph. | Confirmar credenciales y remitente. | 🟡 Pendiente cliente |
| **D-08** | **Identidad de marca** para el PDF: logo, razón social, RUC, dirección, teléfono, pie legal. | El SRS pide formato corporativo. Pedir assets. | 🟡 Pendiente cliente |
| **D-15** | **Alcance del ERP** (§12): ¿el ERP es el de **ORKIN** (maestro de productos/precios) o el de **cada cliente de ORKIN** (inventario de equipos instalados)? | El modelo de datos ya soporta ambos (`ErpConexion.Ambito`). **Recomendación:** empezar por el **ERP de ORKIN** — el de un cliente de ORKIN no tiene los precios ni el maestro de productos de ORKIN, así que no sirve para el cotizador | 🔴 Pendiente cliente |
| **D-19** | **La integración con el ERP es de UNA SOLA DIRECCIÓN: el ERP alimenta los catálogos del cotizador; la web NUNCA escribe en el ERP del cliente.** *"Actualmente no se está pensando que la web actualice el ERP del cliente. Quizás a futuro generar alguna orden de compra, pero ahorita eso está fuera del alcance."* | Se elimina `int.OutboxMensaje` de la Fase 12 (queda documentado en `BASE-DE-DATOS.md` §7.16 como diseño diferido). `ErpConexion.EsSoloLectura` con `CHECK (= 1)`. `IErpConector` solo expone `Leer`. El usuario SQL que se pide al DBA del cliente es `db_datareader` | ✅ **Confirmada por el cliente** (2026-09-27) |
| **D-09** | Numeración oficial de cotización (`N° 0413`) y consecutivo por año. | **Resuelta (2026-09-27):** el formato (plantilla, prefijo, ceros) vive en `TipoConsecutivoCatalogo`; cada número congelado en `dbo.Consecutivo`. Solo se numera lo que la plataforma crea. Ver §5.15/§6.13 de `BASE-DE-DATOS.md` y ADR-019 |
| **D-10** | Hosting: **IIS**, Azure App Service, o Docker + Traefik. Dado SQL Server, IIS es la opción más simple. | IIS + SQL Server, con Docker como alternativa si hay Linux. | 🟡 Pendiente cliente |
| **D-16** | ¿Se necesita **firmas digitales**, **vigencia/expiración** de cotización y **moneda de presentación** (C$ / US$ / ambas)? | Ambas monedas siempre visibles (como el Excel). Vigencia configurable. | 🟢 Pendiente |
| **D-20** *(nueva)* | Si algún día se retoma la **orden de compra**: ¿el DBA del cliente autoriza que escribamos en su ERP, o se resuelve con exportación de archivo? | No bloquea nada. **Fuera de alcance** (D-19). Solo cambia cuando se retome | 🟢 Pendiente |

---

## 4. Arquitectura objetivo

### 4.1 Stack

**Target framework: `net10.0` (.NET 10) en los 8 proyectos. Sin excepciones.**

| Capa | Tecnología |
|---|---|
| Frontend | **Blazor WebAssembly (standalone, .NET 10)** + **MudBlazor 9** (componentes, tema con la paleta del Prototipo D) |
| BFF / Hosting | ASP.NET Core Web (`Orkin.Cotizador.UI.Web`) — mismo patrón BFF que la referencia: cookie de sesión + JWT en memoria server-side |
| Backend / API | ASP.NET Core 10 Web API + Controllers |
| Aplicación | Capa de servicios (`IXService` / `XService`) + repositorios. **Sin CQRS/MediatR** (consistente con la referencia) |
| Dominio | `Core.Domain` POCO puro + **motor de cálculo** sin dependencias de BD ni UI |
| Datos | EF Core 10 (Code First) + migraciones. Provider: **`Microsoft.EntityFrameworkCore.SqlServer`** (D-01). Esquemas: `dbo`, `seg`, `int`, `aud` |
| Auth | **Propia** (D-02): `ASP.NET Core Identity` sobre el esquema `seg` + `JwtBearer` emitido por la propia API + políticas dinámicas por permiso |
| PDF | QuestPDF (comunitario, sin licencia) |
| Correo | `System.Net.Mail` / SendGrid + cola en background |
| Tipo de cambio | `HttpClient` a API del BCN + `BackgroundService` diario + carga manual de respaldo |
| Auditoría | `SaveChangesInterceptor` de EF Core → tabla `aud.RegistroAuditoria` (append-only) |
| Notificaciones en vivo | SignalR (`/hubs/...`) — opcional |
| Pruebas | **xUnit v3** — obligatorio para el motor de cálculo (comparación contra el Excel) |

#### 4.1.1 Versiones de paquetes (fijadas en la Fase 0)

Verificadas contra nuget.org el 2026-09-27. Se registran aquí para que la solución sea reproducible.

| Paquete | Versión | Proyecto | Nota |
|---|---|---|---|
| `MudBlazor` | **9.11.0** | `UI.Shared` | Última estable de la rama 9.x, la que usa la referencia. Compatible con `net10.0` |
| `Microsoft.EntityFrameworkCore` | 10.0.12 | `Core.Application`, `Core.Infrastructure` | Rama 10.x, la pareja de .NET 10 |
| `Microsoft.EntityFrameworkCore.SqlServer` | 10.0.12 | `Core.Infrastructure` | Provider de D-01 |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.12 | `Core.Infrastructure` | Para `dotnet ef` |
| `Microsoft.EntityFrameworkCore.Sqlite` | 10.0.12 | `Core.Infrastructure` | Solo para el **provider de pruebas** del motor de cálculo |
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | 10.0.12 | `Core.Infrastructure` | Tablas de `seg` sobre SQL Server |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.x | `Api` | |
| `Microsoft.AspNetCore.Components.Web` / `.Authorization` / `.WebAssembly*` | 10.0.x | `UI.Shared`, `UI.Web`, `UI.Web.Client` | |
| `Microsoft.AspNetCore.SignalR.Client` | 10.0.x | `UI.Shared` | |
| `Swashbuckle.AspNetCore` | 10.2.3 | `Api` | Referencia usa la misma |
| `System.IdentityModel.Tokens.Jwt` | 8.19.1 | `Core.Application` | Igual que la referencia |
| `Microsoft.IdentityModel.Tokens` | 8.19.1 | `Core.Application` | |
| `QuestPDF` | 2026.9.1 | `Core.Infrastructure` | |
| `DotNetEnv` | 3.2.0 | `Api` | Carga `.env` |
| `xunit.v3` + `Microsoft.NET.Test.Sdk` | 4.0.1 / 17.x | `Tests` | |
| `Microsoft.Data.SqlClient` | 6.x | `Core.Infrastructure` | Solo si se necesita SQL a mano (outbox, consecutivos) |

> **No se usan** (a diferencia de la referencia): `Npgsql.EntityFrameworkCore.PostgreSQL` (D-01),
> `EFCore.NamingConventions` (SQL Server no usa `snake_case`), `Merakodesoft.Auth.*` (D-02),
> `Microsoft.Maui.*` (D-13).
>
> Las versiones exactas de los `Microsoft.AspNetCore.*` se resuelven en la Fase 0 con
> `dotnet add package` para tomar la última 10.0.x publicada; aquí se registra la mayor conocida.

### 4.2 Estructura de solución (réplica de `Mikaela.Prestamos`, sin MAUI)

```
D:\repos\sinco-orkin\
├── Orkin.Cotizador.slnx                      # formato XML nuevo, como la referencia
│
├── Orkin.Cotizador.Core.Domain/              # POCOs, enums, motor de cálculo puro
│   ├── Common/
│   │   ├── BaseEntity.cs                     #   BaseEntity + BaseEntity (sin TenantId)
│   │   └── Enumeraciones.cs                  #   EstadoCotizacion, TipoCliente, Semestre, EstadoAprobacion, AccionAuditoria
│   ├── Catalogos/                            #   hereda de CatalogoBase
│   │   ├── CatalogoBase.cs
│   │   └── Catalogos.cs                      #   TipoServicio, UnidadMedida, CategoriaProducto…
│   ├── Entities/                             #   un archivo por entidad
│   │   ├── Cliente.cs
│   │   ├── Cotizacion.cs
│   │   ├── DetalleInsumo.cs
│   │   ├── DetalleAmortizable.cs
│   │   ├── Producto.cs
│   │   ├── PrecioHistorial.cs
│   │   ├── ActivoAmortizable.cs
│   │   ├── TipoServicio.cs
│   │   ├── TipoCambio.cs
│   │   ├── Aprobacion.cs
│   │   ├── ParametroGeneral.cs
│   │   ├── Consecutivo.cs
│   │   ├── Usuario.cs        Rol.cs        Permiso.cs
│   │   └── AuditLog.cs
│   ├── Cotizacion/
│   │   ├── LineaCotizacion.cs               #   value objects de entrada del motor
│   │   └── ResultadoCotizacion.cs
│   └── MotorCalculo/
│       ├── ICotizadorCalculo.cs
│       └── CotizadorCalculo.cs              #   SIN dependencias: solo RN-01..RN-10
│
├── Orkin.Cotizador.Core.Application/        # servicios, DTOs, interfaces, DI
│   ├── DependencyInjection/
│   │   └── ServiceCollectionExtensions.cs    #   AddApplicationServices()
│   ├── DTOs/
│   │   ├── ClienteDto.cs                    #   ClienteDto + CreateClienteRequest + UpdateClienteRequest
│   │   ├── CotizacionDto.cs
│   │   ├── ProductoDto.cs
│   │   ├── ActivoAmortizableDto.cs
│   │   ├── TipoServicioDto.cs
│   │   ├── TipoCambioDto.cs
│   │   ├── AprobacionDto.cs
│   │   ├── DashboardDto.cs
│   │   └── AuthDto.cs
│   ├── Interfaces/
│   │   ├── IClienteService.cs
│   │   ├── ICotizacionService.cs
│   │   ├── IProductoService.cs
│   │   ├── IActivoAmortizableService.cs
│   │   ├── ITipoServicioService.cs
│   │   ├── ITipoCambioService.cs
│   │   ├── ICurrentUserContext.cs
│   │   ├── INotificacionService.cs          #   correo
│   │   ├── IGeneradorPdfService.cs
│   │   ├── ITipoCambioProvider.cs           #   Manual | BancoCentral
│   │   ├── IAuthService.cs                  #   D-02: emisión propia de JWT
│   │   ├── IErpConnectorFactory.cs          #   Fase 12: conexión con cualquier ERP
│   │   ├── IErpSincronizacionService.cs     #   Fase 12: sincronización de datos
│   │   └── ISecuenciaService.cs             #   consecutivos
│   └── Services/
│       ├── ClienteService.cs                #   + IClienteService.cs
│       ├── CotizacionService.cs
│       ├── ProductoService.cs
│       ├── ActivoAmortizableService.cs
│       ├── TipoServicioService.cs
│       ├── TipoCambioService.cs
│       ├── AprobacionService.cs
│       ├── DashboardService.cs
│       ├── NotificacionService.cs
│       ├── GeneradorPdfService.cs
│       ├── ConsecutivoService.cs
│       └── ManualTipoCambioProvider.cs  BancoCentralTipoCambioProvider.cs  TipoCambioProviderFactory.cs
│
├── Orkin.Cotizador.Core.Infrastructure/     # EF Core, repositorios, servicios externos
│   ├── DependencyInjection/
│   │   └── ServiceCollectionExtensions.cs    #   AddInfrastructure() + AddFileStorage()
│   ├── Options/
│   │   ├── DatabaseOptions.cs
│   │   ├── SmtpOptions.cs
│   │   ├── TipoCambioOptions.cs
│   │   └── CotizadorPdfOptions.cs
│   ├── Persistence/
│   │   ├── CotizadorDbContext.cs            #   45+ DbSet, config en OnModelCreating
│   │   ├── CotizadorDbContextFactory.cs     #   IDesignTimeDbContextFactory (para dotnet ef)
│   │   ├── DbInitializer.cs                 #   static: seed catálogos + admin + consecutivos
│   │   ├── AuditSaveChangesInterceptor.cs    #   ★ motor de auditoría
│   │   └── Migrations/
│   ├── Repository/
│   │   ├── IBaseRepository.cs / BaseRepository.cs
│   │   ├── IUnitOfWork.cs / UnitOfWork.cs
│   │   ├── ClienteRepository.cs / IClienteRepository.cs
│   │   ├── CotizacionRepository.cs / ICotizacionRepository.cs
│   │   └── ... (interfaz e implementación en la MISMA carpeta, como la referencia)
│   └── Services/
│       ├── TenantContext.cs  (si aplica multi-empresa)
│       └── AlmacenamientoArchivoService.cs
│
├── Orkin.Cotizador.Api/                     # API REST — Microsoft.NET.Sdk.Web
│   ├── Program.cs                           #   composition root
│   ├── Dockerfile
│   ├── Authorization/
│   │   ├── Permissions.cs                   #   constantes "Cotizaciones.Read", "Cotizaciones.Approve"…
│   │   ├── PlatformOwner.cs
│   │   └── DynamicPermissionAuthorizationPolicyProvider.cs
│   ├── Controllers/
│   │   ├── AuthController.cs
│   │   ├── ClientesController.cs
│   │   ├── CotizacionesController.cs        #   ★ incluye POST /cotizaciones/{id}/calcular (tiempo real)
│   │   ├── ProductosController.cs
│   │   ├── ActivosAmortizablesController.cs
│   │   ├── TiposServicioController.cs
│   │   ├── TiposCambioController.cs
│   │   ├── AprobacionesController.cs        #   endpoint PÚBLICO con token
│   │   ├── DashboardController.cs
│   │   ├── ParametrosController.cs
│   │   ├── UsuariosController.cs
│   │   ├── RolesController.cs
│   │   ├── PermisosController.cs
│   │   ├── ConsecutivosController.cs
│   │   ├── IntegracionesController.cs         #   Fase 12: conexiones ERP, mapeos, "sincronizar ahora"
│   │   ├── WebhookController.cs               #   Fase 12: POST /api/integraciones/webhook  (público, por token)
│   │   ├── SeedController.cs
│   │   └── HealthController.cs              #   → /health
│   ├── DTOs/ApiResponse.cs                  #   ApiResponse<T> + ApiResponse
│   ├── Handlers/ForwardTokenHandler.cs
│   ├── Hubs/                                #   (Fase 6) NotificacionesHub
│   ├── Middlewares/
│   │   ├── GlobalExceptionMiddleware.cs
│   │   └── InternalApiKeyMiddleware.cs
│   ├── Services/
│   │   ├── CurrentUserService.cs / ICurrentUserService.cs
│   │   ├── TipoCambioBackgroundService.cs   #   (Fase 6) job diario
│   │   ├── NotificacionBackgroundService.cs #   cola de correo con reintentos
│   │   └── Integracion/                      #   Fase 12
│   │       ├── ErpSincronizacionBackgroundService.cs   #  polling: "en vivo"
│   │       ├── OutboxDispatchBackgroundService.cs      #  escritura de vuelta al ERP
│   │       └── ProteccionDeCredenciales.cs              #  Data Protection
│   ├── Swagger/SecurityDocumentFilter.cs
│   └── Properties/launchSettings.json
│
├── Orkin.Cotizador.ApiClient/               # SDK tipado para la UI  (NO va en el .slnx, como la referencia)
│   ├── ICotizadorApiClient.cs
│   ├── CotizadorApiClient.cs
│   ├── CotizadorApiClientOptions.cs
│   ├── Clients/Interfaces/… + Clients/Implementations/…
│   ├── DTOs/ApiResponse.cs
│   ├── Exceptions/ApiException.cs
│   ├── Extensions/HttpResponseMessageExtensions.cs
│   └── Handlers/TokenHandler.cs
│
├── Orkin.Cotizador.UI/
│   ├── Orkin.Cotizador.UI.Shared/           # RCL — MudBlazor 9.11.0 (todas las páginas)
│   │   ├── _Imports.razor
│   │   ├── Routes.razor                     #   Router + AuthorizeRouteView
│   │   ├── Authorization/                   #   DynamicPermission..., PlatformOwner
│   │   ├── Components/                      #   ~30: KpiCard, PermisoGate, dialogs, EstadoBadge
│   │   ├── Layout/
│   │   │   ├── MainLayout.razor (+ .razor.css)      ← sidebar del Prototipo D
│   │   │   ├── NavMenu.razor  (+ .razor.css)       ← grupos "General" / "Próximamente"
│   │   │   ├── NavMenuNode.razor  NavMenuIconMapper.cs
│   │   │   └── LoginLayout.razor                   ← ★ pantalla de login
│   │   ├── Models/  (UserInfo, LoginResult…)  +  Models/DTOs/  (espejo de Application)
│   │   ├── Pages/
│   │   │   ├── Login.razor            @page "/login"          ★ Fase 1
│   │   │   ├── Home.razor             @page "/"
│   │   │   ├── Dashboard.razor        @page "/dashboard"
│   │   │   ├── NotFound.razor  RedirectToLogin.razor
│   │   │   ├── Cotizaciones/
│   │   │   │   ├── Lista.razor            @page "/cotizaciones"
│   │   │   │   ├── Formulario.razor       (nueva / editar / nueva versión)
│   │   │   │   ├── Detalle.razor         @page "/cotizaciones/{id:int}"
│   │   │   │   └── AnularDialog.razor    ResumenDialog.razor  BuscarProductoDialog.razor
│   │   │   ├── AprobacionPublica.razor  @page "/aprobar/{token}"   ★ vista sin login
│   │   │   ├── Auditoria.razor        @page "/auditoria"
│   │   │   ├── Catalogos/Productos.razor  ActivosAmortizables.razor  TiposServicio.razor  Units.razor
│   │   │   ├── Catalogos/TiposCambio.razor
│   │   │   ├── Clientes/Lista.razor  Detalle.razor
│   │   │   └── Admin/Usuarios.razor  Roles.razor  Parametros.razor
│   │   ├── Services/                       # ~20: ApiService + XService
│   │   └── wwwroot/  (app.css con la paleta del Prototipo D, favicon)
│   │
│   ├── Orkin.Cotizador.UI.Web/              # BFF — Microsoft.NET.Sdk.Web
│   │   ├── Program.cs  Dockerfile  appsettings*.json
│   │   ├── Components/App.razor  Pages/Error.razor  _Imports.razor
│   │   ├── Controllers/
│   │   │   ├── AuthController.cs           #  login / logout / refresh / me
│   │   │   ├── NavigationController.cs     #  menú por rol/permisos
│   │   │   └── ProxyController.cs          #  BFF: /api/proxy/{**path}
│   │   └── Services/ServerAccessTokenStore.cs  WebAppInfoService.cs  FormFactor.cs
│   │
│   ├── Orkin.Cotizador.UI.Web.Client/       # WASM — Microsoft.NET.Sdk.BlazorWebAssembly
│   │   ├── Program.cs  _Imports.razor
│   │   ├── Handlers/CookieCredentialsHandler.cs
│   │   └── wwwroot/appsettings*.json
│
├── Orkin.Cotizador.Tests/                   # xUnit
│   ├── MotorCalculo/                        # ★ goldens contra el Excel
│   │   ├── CotizadorResidencialTests.cs
│   │   ├── CotizadorEmpresarialTests.cs
│   │   └── Fixtures/ExcelCasosDePrueba.json
│   └── Application/  (Casos de uso CotizacionService, AprobacionService…)
│
├── docs/                                    # documentación por módulo (patrón de la referencia)
├── scripts/seed/                            # ★ la semilla: un JSON por catálogo, idempotente por Codigo (§5.20)
├── scripts/                                 # .sql de corrección, utilidades
├── .github/workflows/                       # deploy-staging.yml, deploy-production.yml
├── .env.Development.example  y .env.*.example por entorno   # ★ plantillas; la copia local es la que corre
├── .env.Development  .env.local …             # NUNCA se versionan
├── docker-compose.yml  docker-compose.prod.yml
├── dev-start.ps1  dev-start.sh
├── nuget.config
└── .gitignore  .dockerignore
```

> **`.env` y `scripts/seed` viven en la raíz de la solución, no dentro de un proyecto.** Los `.env` se
> cargan subiendo directorios hasta encontrar la raíz —los lee `CargadorArchivoEnv`, con
> `.env.{ENTORNO}` pisando a `.env`—, y la semilla resuelve `scripts/seed` con la misma regla. Por eso
> un `dotnet run` funciona desde la raíz y desde la carpeta de cualquier proyecto, y el contenedor
> encuentra todo en `/app`.

### 4.3 Grafo de dependencias

```
Orkin.Cotizador.Core.Domain  (hoja)
        ▲
        │  Orkin.Cotizador.Core.Infrastructure
        │        ▲
        │        │        Orkin.Cotizador.Core.Application
        │        ▲        ▲
        │        │        │
        │        └────────┼──── Orkin.Cotizador.Api
        │                 │
        │        Orkin.Cotizador.UI.Shared  (hoja)
        │                 ▲
        │        Orkin.Cotizador.UI.Web.Client (WASM)
        │                 ▲
        └─────────────────┴──── Orkin.Cotizador.UI.Web (BFF)

Orkin.Cotizador.ApiClient  →  Orkin.Cotizador.Core.Application
                             (SDK tipado, fuera del .slnx, se empaqueta en build/LocalPackages)
```

### 4.4 Convenciones

> Heredadas de `Mikaela.Prestamos` donde tiene sentido, **ajustadas a SQL Server y a seguridad propia** donde no.

| Tema | Convención |
|---|---|
| Solución | `.slnx` (XML) con `Debug` / `Release` / **`Staging`** declaradas en **cada** `.csproj` |
| Configuraciones | `Debug;Release;Staging` como triple explícito, nunca implícita |
| **Base de datos** | **SQL Server.** Provider `Microsoft.EntityFrameworkCore.SqlServer`. **Sin** `EFCore.NamingConventions` (SQL Server no usa `snake_case`) |
| **Esquemas** | `dbo` (negocio) · `seg` (seguridad) · `int` (integraciones/ERP) · `aud` (auditoría). Todos con prefijo explícito en las entidades EF |
| **Nombres de tabla/columna** | `PascalCase`, singular, sin tildes ni eñes: `Cotizacion`, `DetalleInsumo`, `PrecioUnitario` |
| **Dinero** | `decimal(18,6)` para costos, precios y totales (**el Excel tiene precios con 6 decimales**: `9.999444`, `415.261225`; redondear a 2 decimales rompería la paridad). Sin redondeo intermedio; el redondeo a 2 decimales ocurre **solo al presentar** (PDF, UI) |
| **Tipo de cambio** | `decimal(18,6)` (el histórico va de `33.8408` a `36.6243`) |
| **Margen** | `decimal(5,4)` como **fracción** (`0.0550` = 5.5 %), con `CHECK` de rango `>= 0 AND <= 1`. Corrige **H-02** |
| **Cantidades** | `decimal(18,4)` — admiten fracciones: ml, gr, kg, H.H, km, yardas |
| **Porcentaje plantilla** | `decimal(9,6)` |
| **Enums** | `tinyint` con FK a tabla de catálogo (`EstadoCotizacion`, `Semestre`, `OrigenDato`…). Se mapean a enums C# con `HasConversion<...>()` en un solo lugar |
| **Id** | `uniqueidentifier` con default `NEWSEQUENTIALID()` |
| **Concurrencia** | `rowversion` en tablas editables |
| **Fechas** | `date` para fechas de negocio, `datetime2(3)` para marcas de tiempo. **Siempre UTC** en BD; conversión a `America/Managua` solo al presentar |
| **Collation** | `SQL_Latin1_General_CP1_CI_AS` (case-insensitive) para que las búsquedas de productos no fallen por tildes ni mayúsculas |
| **Baja lógica** | `IsActive bit` en catálogos; las cotizaciones nunca se borran, se **anulan** |
| Dominio | Sin DataAnnotations. Configuración 100 % en `OnModelCreating` |
| Auditoría base | `CreatedAt`/`UpdatedAt` en UTC, `CreatedBy`/`UpdatedBy`, `IsActive` |
| Navegaciones | `= null!` si la FK es obligatoria; `?` sin inicializador si es opcional |
| Colecciones | Siempre `ICollection<T> … = new List<T>()` |
| Patrón de aplicación | Capa de servicios: `Controller → IXService (Application) → IXRepository (Infrastructure) → DbContext`. **Sin MediatR, sin FluentValidation** (validación manual con `InvalidOperationException` → HTTP 400) |
| Servicios | Métodos `Async` con `CancellationToken cancellationToken = default` **siempre al final**; retornos `?` cuando puede no existir |
| DTOs | `class` con `{ get; set; }` (no `record`). `XDto` / `CreateXRequest` / `UpdateXRequest` |
| Repositorios | Interfaz e implementación **en la misma carpeta** `Repository/`; todas los métodos `virtual` |
| Migraciones | En `Persistence/Migrations/`; `IDesignTimeDbContextFactory` para que `dotnet ef` funcione sin DI |
| Endpoints | `[Authorize]` en clase + `[Authorize(Policy = Permissions.X)]` por acción; `api/[controller]`; `{id:guid}`; `CreatedAtAction` en POST; `Conflict` en duplicados |
| Respuesta | Envelope `ApiResponse<T> { Data, Success, Message, Error, TraceId }` con factories `Ok()` / `Failure()` |
| Errores | `GlobalExceptionMiddleware`: `InvalidOperationException` → 400, `UnauthorizedAccessException` → 403, resto → 500 (mensaje real solo en dev/qa/stg) |
| Permisos | Constantes en `Permissions.cs` con patrón `Entidad_Accion` en C# ↔ `Entidad.Accion` en el token. Política dinámica: **el nombre de la política ES el permiso**; `A|B` = OR |
| Config | **Secretos fuera de `appsettings.json`**: `DotNetEnv` + `.env` + variables de entorno |
| Despliegue | Contenedores en `:8080`, Traefik termina TLS, migraciones desde el workflow (la API **no** ejecuta `EnsureCreated` en prod) |
| Puerto local | `3031` para la API (coherente con la referencia) |

---

## 5. Modelo de datos

> 📄 **Documento detallado: [`docs/BASE-DE-DATOS.md`](docs/BASE-DE-DATOS.md)**
> Contiene la especificación completa: tablas, columnas, tipos, nulabilidad, valores por defecto,
> índices, claves foráneas, restricciones `CHECK`, catálogos sembrados, scripts DDL y el diagrama ER.

Resumen de los **4 esquemas** en SQL Server:

| Esquema | Contenido |
|---|---|
| `dbo` | Negocio: `Cliente`, `Cotizacion`, `DetalleInsumo`, `DetalleAmortizable`, catálogos, `TipoCambio`, `Consecutivo` |
| `seg` | Seguridad propia: `Usuario`, `Rol`, `Permiso`, `RolPermiso`, `UsuarioRol`, `Sesion`, `TokenAprobacion` |
| `int` | Motor ETL de integración con ERP (**solo lectura**, D-19): `ErpConexion`, `ErpFlujo`, `ErpOrigen`, `ErpMapeoCampo`, `ErpValidacion`, `ErpRechazo`, `ErpDestino`, `ErpDependencia`, `SincronizacionEjecucion`, `SincronizacionDetalle`, `ErpSimulacion`, `ErpAlerta`, `EventoRecibido` |
| `aud` | Auditoría: `RegistroAuditoria` (append-only) |

Mapa conceptual:

```
seg.Usuario ──N:N──► seg.Rol ──N:N──► seg.Permiso
                          │
Auditoría  CreatedBy/UpdatedBy  ──►  seg.Usuario

dbo.Cliente ──1:N──► dbo.Cotizacion ──1:N──► dbo.DetalleInsumo ──N:1──► dbo.Producto
                            │                        │
                            │                        └──N:1──► dbo.PrecioProducto   (histórico)
                            ├──1:N──► dbo.DetalleAmortizable ──N:1──► dbo.ActivoAmortizable
                            ├──1:N──► dbo.Aprobacion
                            ├──1:N──► dbo.CotizacionVersion  (versionado, D-03)
                            └──N:1──► dbo.TipoCambio ──1:N──► dbo.TipoCambioHistorial

dbo.Producto ──N:1──► dbo.UnidadMedida        dbo.ActivoAmortizable ──N:1──► dbo.UnidadMedida
dbo.Producto ──N:1──► dbo.CategoriaProducto   dbo.TipoServicio ──1:N──► dbo.TipoServicioMargen

int.ErpConexion ──1:N──► int.ErpFlujo ──1:N──► int.ErpOrigen / ErpMapeoCampo / ErpValidacion / ErpDestino
int.ErpFlujo ──1:N──► int.SincronizacionEjecucion ──1:N──► int.SincronizacionDetalle / int.ErpRechazo
int.ErpFlujo ──1:N──► dbo.*.CodigoExterno  (OrigenDato = 2 · ErpConexionId · HashContenido)
int.ErpConexion ──1:N──► int.EventoRecibido   (webhooks = disparo inmediato)
                                          ──── SOLO LECTURA: nada sale hacia el ERP (D-19)
```

**Estados de cotización** (alineados con los badges del Prototipo D):
`Borrador` · `EnAprobacion` · `Aprobada` · `Rechazada` · `Enviada` · `Vencida` · `Anulada` · `ConvertidaEnServicio`

**Principio de diseño clave:** toda fila de catálogo tiene `OrigenDato` (`Manual` | `Erp`) y
`CodigoExterno` + `ErpConexionId`. Eso permite que mañana los catálogos se llenen **en vivo desde el
ERP** sin cambiar el modelo (ver Fase 12).

---

## 6. Plan de fases y tareas

### Leyenda de estados

| Estado | Significado |
|---|---|
| ⬜ **Pendiente** | No iniciado |
| 🟡 **En progreso** | En ejecución |
| 🔵 **En revisión** | Hecho, esperando validación |
| ✅ **Completado** | Implementado y verificado |
| ⏸️ **Postergado** | Fuera del alcance actual |
| 🔴 **Bloqueado** | Depende de una decisión o insumo externo |

> **Regla de trabajo:** no se cierra una fase con tareas 🔴 o 🟡 abiertas. Dentro de una fase las tareas pueden avanzar en paralelo.

---

### Fase 0 — Cimientos e infraestructura

> Objetivo: solución compilable, base de datos levantada, entorno reproducible.

| # | Tarea | Entregable | Estado |
|---|---|---|---|
| 0.0 | **Especificar el modelo de datos** (tablas, columnas, índices) | 📄 [`docs/BASE-DE-DATOS.md`](docs/BASE-DE-DATOS.md) | 🔵 En revisión |
| 0.1 | Confirmar decisiones **D-03 a D-16** con el cliente | Tabla §3.2 resuelta | 🔴 |
| 0.2 | Crear `Orkin.Cotizador.slnx` con los **8** proyectos (sin MAUI) | Solución con `Debug;Release;Staging` | ✅ |
| 0.3 | `Core.Domain`: `BaseEntity`, enums, entidades POCO | Código compilando | ✅ |
| 0.4 | `Core.Application` + `Core.Infrastructure`: DTOs, interfaces, DI, `CotizadorDbContext` con los 4 esquemas | `AddApplicationServices()` / `AddInfrastructure()` | ✅ |
| 0.5 | `Api`: `Program.cs`, Swagger, `GlobalExceptionMiddleware`, `ApiResponse<T>`, `HealthController` | `/swagger` y `/health` verdes | ✅ |
| 0.6 | `UI.Shared` + `UI.Web` + `UI.Web.Client`: layout base, tema MudBlazor con la paleta del Prototipo D, `Home` de humo | App carga y muestra el sidebar | ⬜ |
| 0.7 | Migración inicial de EF Core (desde `docs/BASE-DE-DATOS.md`) + `DbInitializer` (catálogos vacíos, admin, roles, permisos, consecutivos) | BD creada con `dotnet ef` | ✅ |
| 0.8 | `.env.dev.example`, `.env.dev`, `dev-start.ps1`, `dev-start.sh`, `docker-compose.yml` (SQL Server + API + web) | `dev-start` levanta todo | ⬜ |
| 0.9 | `nuget.config`, `.gitignore`, `.dockerignore`, `Dockerfile` × 2, `docker-compose.prod.yml` | Base de despliegue | ⬜ |
| 0.10 | Git init + estructura de ramas (`main` / `staging`) + `.github/workflows` esqueleto | Repo listo | ⬜ |
| 0.11 | `Orkin.Cotizador.Tests` con un test de humo del motor de cálculo | `dotnet test` verde | ⬜ |
| 0.12 | Tema MudBlazor: traducir la paleta del Prototipo D + `Layout/MainLayout` y `NavMenu` con grupos | Sidebar fiel al prototipo | ⬜ |
| 0.13 | Script de arranque de SQL Server + base creada + usuario de la app | `dev-start` levanta la BD | ⬜ |

---

### Fase 1 — Autenticación, roles y permisos

> El login **no** aparece en los prototipos pero está en el alcance. Sin esto ninguna otra pantalla es navegable.

| # | Tarea | Entregable | Estado |
|---|---|---|---|
| 1.1 | Definir matriz de roles y permisos (ver abajo) | Tabla §6.1 aprobada | ⬜ |
| 1.2 | `AuthController` + login/refresh/logout (D-02: **Identity propio**, sin plataforma externa) | Token emitido y validado | ⬜ |
| 1.3 | `UI.Shared/Pages/Login.razor` + `LoginLayout` | Pantalla de login responsive | ⬜ |
| 1.4 | BFF: `ProxyController`, `ServerAccessTokenStore`, cookie de sesión | `api/proxy/*` reenvía con Bearer | ⬜ |
| 1.5 | `Permissions.cs` + `DynamicPermissionAuthorizationPolicyProvider` | Policies dinámicas | ⬜ |
| 1.6 | `NavigationController` → menú filtrado por permisos | Sidebar solo con lo permitido | ⬜ |
| 1.7 | Entidades `Usuario` / `Rol` / `Permiso` + CRUD de admin | Gestión de usuarios y roles | ⬜ |
| 1.8 | `RouteGuard` / `RedirectToLogin` / `NotAuthorized` | Rutas protegidas | ⬜ |

#### 6.1 Matriz de roles (propuesta inicial)

| Rol | Permisos clave |
|---|---|
| **Administrador** | Todos (`Cotizaciones.*`, `Catalogos.*`, `Admin.*`, `Auditoria.Read`, `Parametros.Manage`) |
| **Vendedor / Cotizador** | `Cotizaciones.Read`, `.Create`, `.Update`, `.Send`, `Clientes.Read/Create/Update`, `Catalogos.Read`, `Dashboard.Read` |
| **Aprobador / Gerencia** | `Cotizaciones.ReadAll`, `.Approve`, `.Reject`, `Auditoria.Read`, `Dashboard.Read` |
| **Técnico** | `Cotizaciones.Read` (solo las asignadas), `Clientes.Read` |
| **Solo lectura** | `*.Read` únicamente |

---

### Fase 2 — Catálogos y migración de datos desde el Excel

> Objetivo: que la web tenga los mismos datos que el Excel, pero administrables.

| # | Tarea | Detalle | Estado |
|---|---|---|---|
| 2.1 | CRUD **Productos** (`Producto`, `UnidadMedida`, `CategoriaProducto`) | 164 filas de `Tabla1912` + 113 de `Tabla10` | ⬜ |
| 2.2 | CRUD **Activos amortizables** con `VidaUtilMeses` **obligatoria** | 19 filas de `Tabla9` | ⬜ |
| 2.3 | CRUD **Tipos de servicio** con `MargenSugerido` | 11 filas de `TablaServicios` | ⬜ |
| 2.4 | CRUD **Tipo de cambio** (alta manual + histórico) | 1 643 filas de `Tabla2` | ⬜ |
| 2.5 | **Script de importación** del Excel → tablas (idempotente, con reporte de rechazos) | Carga los 4 catálogos | ⬜ |
| 2.6 | **Depuración H-10**: consolidar duplicados y decidir qué entra, qué se marca `Activo=false` | Lista de decisiones con el cliente | ⬜ |
| 2.7 | `PrecioHistorial` + pantalla de vigencia de precios (**H-09**) | Ver precio vigente e histórico | ⬜ |
| 2.8 | **H-05**: validación que impide cotizar con producto sin precio | Bloqueo con mensaje claro | ⬜ |
| 2.9 | `ParametroGeneral` (umbrales, flags, textos de correo, identidad de empresa) | CRUD de parámetros | ⬜ |
| 2.10 | `Consecutivo` + `ConsecutivoService` (numeración `N° 0001`) | Numeración automática | ⬜ |

---

### Fase 3 — Motor de cálculo (corazón del sistema)

> **Tarea crítica.** Debe replicar las fórmulas del Excel al centavo. Sin esto no hay migración posible.

| # | Tarea | Detalle | Estado |
|---|---|---|---|
| 3.1 | `MotorCalculo/CotizadorCalculo.cs` — **RN-01 a RN-08** | Clase pura, sin dependencias | ⬜ |
| 3.2 | Resolver **H-02**: margen como fracción `decimal(5,4)` + validación de rango | Semántica inequívoca | ⬜ |
| 3.3 | Resolver **H-01 / RN-09**: `ITipoCambioProvider` → *último valor ≤ fecha*, con fallback configurable | Nunca `#N/A` | ⬜ |
| 3.4 | Resolver **H-03 / D-03**: cálculo empresarial por semestre | Independentemente por semestre | 🔴 |
| 3.5 | Resolver **H-04**: vida útil obligatoria en amortizables; error si `Meses ≤ 0` con cantidad > 0 | Sin división por cero | ⬜ |
| 3.6 | `ITipoCambioProvider` con UTC/tz de Nicaragua (`America/Managua`) | Zona horaria correcta | ⬜ |
| 3.7 | **Tests golden master**: 10–15 cotizaciones reales extraídas del Excel, valores esperados al centavo | `ExcelCasosDePrueba.json` | ⬜ |
| 3.8 | Endpoint `POST /api/cotizaciones/calcular` (cálculo en vivo, sin persistir) | Respuesta < 300 ms | ⬜ |
| 3.9 | Prueba de equivalencia: ejecutar el motor sobre los casos y cuadrar contra el Excel | Reporte de paridad | ⬜ |

---

### Fase 4 — Cotizador (API + UI)

> El módulo central. Sigue el Prototipo D al pie de la letra.

| # | Tarea | Detalle | Estado |
|---|---|---|---|
| 4.1 | Entidades de cotización + `CotizacionService` (crear, editar, versionar, anular) | `Cotizacion`, `DetalleInsumo`, `DetalleAmortizable` | ⬜ |
| 4.2 | `CRUD Clientes` (residencial / empresarial) + ficha e historial | Pestaña de clientes del prototipo | ⬜ |
| 4.3 | `Pages/Cotizaciones/Lista.razor` — filtros + tabla con **N°, Cliente, Versión, Servicio, Fecha, Total US$, Estado, Acciones** | Fiel a `proto3` líneas 182-204 | ⬜ |
| 4.4 | `Pages/Cotizaciones/Formulario.razor` como **overlay/página dedicada** con header y barra inferior sticky | Fiel a `proto3` líneas 242-298 | ⬜ |
| 4.5 | Bloque **Insumos consumibles** (Producto, Unidad, Precio C$, Cant., Subtotal, ✕) + `BuscarProductoDialog` con filtro en vivo | Fiel a `proto3` 262-272 | ⬜ |
| 4.6 | Bloque **Activos amortizables** (Activo, Precio, Cant., Vida útil, Valor mensual) | Fiel a `proto3` 273-281 | ⬜ |
| 4.7 | Bloque **Margen y tipo de cambio** con TC read-only (fecha + valor) y recálculo en vivo | Fiel a `proto3` 283-291 | ⬜ |
| 4.8 | Barra inferior con total en vivo `C$ X ≈ $ Y` + `Ver resumen completo` + `Enviar a aprobación` | Fiel a `proto3` 292-297 | ⬜ |
| 4.9 | `ResumenDialog` — desglose de insumos + amortizables + margen + total | Fiel a `proto3` 310-330 | ⬜ |
| 4.10 | Modal **Anular** con confirmación | Fiel a `proto3` 333-342 | ⬜ |
| 4.11 | **Versionado**: editar una cotización aprobada crea `Version+1` conservando la anterior | Ref. SRS §5.1 | ⬜ |
| 4.12 | **Cálculo en vivo** de totales al cambiar cantidades/margen (sin ir al servidor en cada tecla) | UX fluida | ⬜ |
| 4.13 | **Flujo empresarial por semestre** en el formulario (dos bloques de insumos) | Según D-03 | 🔴 |
| 4.14 | `Pages/Cotizaciones/Detalle.razor` — solo lectura, versiones, historial de aprobaciones | Ref. SRS §10 | ⬜ |
| 4.15 | Paginación y filtros en servidor del historial | No traer todo a memoria | ⬜ |
| 4.16 | Exportar a **Excel** (`.xlsx`) del histórico filtrado | Reporte operativo | ⬜ |
| 4.17 | Estados y badges: `Borrador`, `EnAprobacion`, `Aprobada`, `Rechazada`, `Anulada` | Colores del prototipo | ⬜ |

---

### Fase 5 — PDF y envío al cliente

| # | Tarea | Detalle | Estado |
|---|---|---|---|
| 5.1 | `GeneradorPdfService` (QuestPDF) con plantilla corporativa | Logo, razón social, RUC, dirección, pie legal | ⬜ |
| 5.2 | Identidad de marca parametrizada (`ParametroGeneral` / `CotizadorPdfOptions`) | Sin recompilar para cambiar logo | 🔴 (D-08) |
| 5.3 | PDF con desglose de insumos, amortizables, costo, margen, total C$ y US$, TC aplicado | Lista para enviar al cliente | ⬜ |
| 5.4 | Numeración y leyenda de versión en el PDF | Trazabilidad | ⬜ |
| 5.5 | `GET /api/cotizaciones/{id}/pdf` (descarga) y vista previa en pantalla | Botón `Vista previa PDF` | ⬜ |
| 5.6 | `NotificacionService` + plantillas de correo (`CorreoAprobacion`, `CorreoCliente`) | Editables desde administración | ⬜ |

---

### Fase 6 — Flujo de aprobación

| # | Tarea | Detalle | Estado |
|---|---|---|---|
| 6.1 | `ReglaAprobacionService` — reglas por margen mínimo y por monto máximo (parametrizables) | Según D-05 | 🔴 |
| 6.2 | `Aprobacion` + token de un solo uso, con expiración (72 h por defecto) y hash almacenado | Seguridad | ⬜ |
| 6.3 | Endpoint **público** `GET|POST /api/aprobacion-publica/{token}` — sin login | El aprobador no inicia sesión | ⬜ |
| 6.4 | `Pages/AprobacionPublica.razor` — resumen + `Aprobar` / `Rechazar` + comentario | Fiel a `proto3` 209-227 | ⬜ |
| 6.5 | Envío automático del correo al aprobador con el enlace seguro | SMTP | ⬜ |
| 6.6 | Notificación al cotizador al aprobar/rechazar (correo + SignalR si aplica) | Feedback inmediato | ⬜ |
| 6.7 | Invalidación del token tras el uso; pantalla de token vencido/usado | Anti-reuso | ⬜ |
| 6.8 | Vista de cotizaciones pendientes de aprobación en el dashboard | Filtro por rol | ⬜ |
| 6.9 | `TipoCambioBackgroundService` (job diario) + `BancoCentralTipoCambioProvider` | Según D-06 | ⬜ |
| 6.10 | Prueba E2E: crear → dispara regla → llega correo → clic → aprueba → queda auditado | Verificado | ⬜ |

---

### Fase 7 — Auditoría

| # | Tarea | Detalle | Estado |
|---|---|---|---|
| 7.1 | `AuditLog` + `AuditSaveChangesInterceptor` (captura antes/después en JSON) | Automático, sin intervención del código de negocio | ⬜ |
| 7.2 | Marcar entidades auditables (`Cotizacion`, `Producto`, `ActivoAmortizable`, `TipoServicio`, `TipoCambio`, `Usuario`, `ParametroGeneral`) | Extensible | ⬜ |
| 7.3 | Garantía **append-only**: sin `Update` ni `Delete` desde la aplicación | Requisito no funcional | ⬜ |
| 7.4 | `Pages/Auditoria.razor` con filtros por entidad, usuario, rango de fechas y acción | Fiel a `proto3` 229-237 | ⬜ |
| 7.5 | IP y agente de origen de cada acción | Trazabilidad completa | ⬜ |
| 7.6 | Auditoría de la vista pública de aprobación (usuario = "Sistema/Aprobador externo") | Trazabilidad completa | ⬜ |

---

### Fase 8 — Dashboard y reportes

| # | Tarea | Detalle | Estado |
|---|---|---|---|
| 8.1 | `Pages/Dashboard.razor` — 4 KPIs: creadas, valor cotizado, pendientes de aprobación, tasa de aprobación | Fiel a `proto3` 156-161 | ⬜ |
| 8.2 | Tabla **Últimas cotizaciones** (N°, Cliente, Servicio, Total US$, Estado) | Fiel a `proto3` 162-170 | ⬜ |
| 8.3 | Reporte: valor cotizado vs. aprobado por mes (C$ y US$) | Gerencia | ⬜ |
| 8.4 | Reporte: margen promedio real vs. deseado por tipo de servicio | Control interno | ⬜ |
| 8.5 | Reporte: top de tipos de servicio y de clientes | Comercial | ⬜ |
| 8.6 | Exportar reportes a Excel y PDF | Contabilidad | ⬜ |
| 8.7 | Filtros por período, vendedor y estado | | ⬜ |

---

### Fase 9 — Endurecimiento, despliegue y migración definitiva

| # | Tarea | Detalle | Estado |
|---|---|---|---|
| 9.1 | Permisos granulares (p. ej. quién puede cambiar el margen mínimo) | Restricción gerencial | ⬜ |
| 9.2 | Vencimiento automático de cotizaciones (`BackgroundService`) | Estado `Vencida` | ⬜ |
| 9.3 | Búsqueda y reutilización de cotizaciones anteriores como plantilla | SRS §5.4 | ⬜ |
| 9.4 | Límite de peticiones (rate limiting) + endurecimiento de CORS | Seguridad | ⬜ |
| 9.5 | Respaldo automático de la base de datos | Requisito no funcional | ⬜ |
| 9.6 | `.github/workflows/deploy-staging.yml` y `deploy-production.yml` | Self-hosted, como la referencia | ⬜ |
| 9.7 | Plan de **retiro del Excel**: 2 semanas en paralelo, comparación de totales, apagado | Cierre del proyecto | ⬜ |
| 9.8 | Manual de usuario por rol | Entrega | ⬜ |
| 9.9 | Plan de reversión (rollback) documentado | Continuidad | ⬜ |

---

### Fase 10 — Optimización de campo (responsive web)

> Los técnicos de ORKIN trabajan con el celular en el sitio del cliente. Esta fase **no** incluye app
> móvil nativa: la web del Prototipo D ya tiene el patrón `topbar de chips` para móvil (decisión D-13).

| # | Tarea | Detalle | Estado |
|---|---|---|---|
| 10.1 | Auditoría de la UI responsive en móvil y tablet (breakpoint 760 px del Prototipo D) | Verificación real | ⬜ |
| 10.2 | Ajustes de UX según feedback de usuarios reales | Iteración | ⬜ |
| 10.3 | Modo "trabajo en campo" en la web: captura de cantidades sobre la cotización asignada, sin conexión lenta | Caso de uso | ⬜ |
| 10.4 | PWA instalable (manifest + service worker) para técnicos | Instalable desde el navegador | ⬜ |

---

### Fase 11 — Módulos operativos (post-cotizador)

> Se agregarán cuando el cotizador esté en producción y sea la fuente de verdad.

| # | Tarea | Detalle | Estado |
|---|---|---|---|
| 11.1 | Calendario de trabajos (agenda por técnico y fecha) | Ítem "Próximamente" del Prototipo D | ⬜ |
| 11.2 | Plantillas de actividades / checklists por tipo de servicio | Ítem "Próximamente" del Prototipo D | ⬜ |
| 11.3 | Órdenes de trabajo: cotización aprobada → orden → ejecución | Cierra el ciclo | ⬜ |
| 11.4 | Facturación y cobranza | Emisión y seguimiento | ⬜ |

---

### Fase 12 — Integración con ERP del cliente 🔌

> **Alcance (D-19):** **una sola dirección.** El ERP alimenta los catálogos del cotizador con datos
> reales —alta y baja de productos, cambios de precios y costos, activos, tipo de cambio— y **la web
> nunca escribe en el ERP del cliente**. La generación de órdenes de compra queda **fuera de
> alcance**; el diseño está anotado en `BASE-DE-DATOS.md` §7.16 para no repetirlo.
>
> Objetivo: que los catálogos (productos, activos, tipos de servicio, tipo de cambio) y los datos
> maestros se llenen **en vivo y en directo** desde el ERP, sea cual sea.
> Se modela como un **motor ETL**: cada flujo tiene **origen** (tabla · vista · consulta SQL · API ·
> archivo) → **transformación** (campo a campo, ~20 transformaciones) → **validación** (reglas con
> bandeja de rechazos) → **destino** (`dbo.*`, con *upsert* por clave de negocio y detección por hash).
>
> 📄 **Diseño completo: [`docs/INTEGRACION-ERP.md`](docs/INTEGRACION-ERP.md)** ·
> Modelo de datos: [`docs/BASE-DE-DATOS.md`](docs/BASE-DE-DATOS.md) §7
>
> **Ya está resuelto en el diseño:** `OrigenDato`, `CodigoExterno` y `HashContenido` en todos los
> catálogos (tarea 12.5 ya no requiere migración). Requiere confirmar **D-15** (de quién es el ERP).

| # | Tarea | Detalle | Estado |
|---|---|---|---|
| 12.1 | Confirmar **D-15**: ERP de ORKIN o ERP de cada cliente de ORKIN. Listar el ERP concreto (SAP, Odoo, Dynamics, Contpaq, propio…) | Definición del alcance | 🔴 |
| 12.2 | Esquema `int` completo: `ErpFlujo`, `ErpOrigen`, `ErpMapeoCampo`, `ErpValidacion`, `ErpDestino`, `ErpDependencia` | Migración | ⬜ |
| 12.3 | `int.ErpConexion` + CRUD + cifrado (`Data Protection`) + "probar conexión". **`EsSoloLectura` fijo en `1` (D-19)** | "Conectar ERP" en Administración | ⬜ |
| 12.4 | **`IErpConector`**: solo `LeerPagina(entidad, cursor)`. **Sin métodos de escritura** | Abstracción "sea cual sea" | ⬜ |
| 12.5 | `SqlServerErpConector` (el primero; los demás copian el patrón) | | ⬜ |
| 12.6 | Motor de transformación: `TransformacionPipeline` con el catálogo de ~20 operaciones | Código puro + tests | ⬜ |
| 12.7 | Motor de validación + `int.ErpRechazo` + bandeja de rechazos en la UI | Nada falla en silencio | ⬜ |
| 12.8 | Cargador: *upsert* por clave, comparación por `HashContenido`, historial en `PrecioProducto`, y **alta/baja** con aviso de "producto en uso" | | ⬜ |
| 12.9 | `ErpOrquestador`: extrae → transforma → valida → carga → registra | | ⬜ |
| 12.10 | **`int.ErpSimulacion`**: el botón "Simular" con grilla de resultados | El paso que evita arruinar un catálogo | ⬜ |
| 12.11 | `ErpSincronizacionBackgroundService`: scheduler + disparo por evento + reclamo atómico | "En vivo" | ⬜ |
| 12.12 | `WebhookController` (público, por token, con HMAC) → `int.EventoRecibido` | Inmediatez real | ⬜ |
| 12.13 | `int.ErpDependencia`: orden de ejecución entre flujos (TC, unidades y categorías antes que productos) | | ⬜ |
| 12.14 | Pantalla de monitoreo: corridas, contadores, rechazos, alertas, detalle de cambios | Operabilidad | ⬜ |
| 12.15 | Alertas por correo: conexión fallida, 5 fallos seguidos, muchos rechazos, producto desactivado en uso | | ⬜ |
| 12.16 | Conectores adicionales (PostgreSQL, REST, Odoo, MySQL, archivo) | Uno por ERP real | ⬜ |
| 12.17 | Pruebas con un ERP de mentira (contenedor) + integración de punta a punta, incluida la prueba de que **no** se puede escribir en el ERP | | ⬜ |
| 12.18 | Manual de operación: qué revisar cuando algo falla | `docs/DESPLIEGUE.md` | ⬜ |

> **No hay tarea de escritura al ERP.** Es la decisión D-19, y está reforzada por tres capas: el
> `CHECK (EsSoloLectura = 1)` en la base, `IErpConector` sin métodos de escritura, y el usuario SQL
> que pedimos al DBA del cliente es `db_datareader`.

**Orden de integración de las entidades** (`INTEGRACION-ERP.md` §1.2):

| # | Entidad | Por qué en ese orden |
|---|---|---|
| 1 | **Unidad de medida** | Maestro. Sin `LT` / `ML` / `UND` no se guarda un producto |
| 2 | **Categoría de producto** | Maestro. Corre antes que productos para que el enriquecimiento no cree duplicados |
| 3 | **Producto** (alta y baja) | Sin productos no hay cotización. Es el bloqueante |
| 4 | **Precio de producto** (precio de venta y costo) | Es el valor de la integración: lo que cambia seguido |
| 5 | **Tipo de cambio** | Sin TC no hay conversión a US$. Listo antes de las 8 a. m. |
| 6 | **Activo amortizable** | Necesario para la depreciación mensual (Fase 3) |
| 7 | **Tipo de servicio** | Cambia muy poco. Puede seguir editándose a mano |
| 8 | **Cliente** | Fuera del cotizador; relevante en el módulo de clientes (Fase 11) |

**Frecuencia de sincronización — recomendación cerrada** (`docs/INTEGRACION-ERP.md` §4):

| Nivel | Configuración | Latencia | Cuándo |
|---|---|---|---|
| ✅ **Por defecto** | **Híbrido**: evento (webhook) + programado cada **5 min** | 2–5 s con evento, ≤ 5 min sin él | Catálogos de productos, activos y servicios |
| Alta | Programado cada **60 s** + evento | ≤ 1 min | Solo si el cliente exige ver el precio al instante |
| Máxima | Programado cada **30 s** + evento | ≤ 30 s | Solo con justificación escrita. La UI advierte |
| Manual | Botón "Sincronizar ahora" | — | Al configurar, o en temas de baja carga |

> El sondeo de 5 min **no es el plan A**: es la red de seguridad. El modo `Completa` (full) **nunca**
> se programa: solo se usa una vez, al activar el flujo. `IntervaloSegundos` tiene `CHECK >= 10` y
> los flujos se ejecutan con **jitter** (`DesfaseSegundos`) para no golpear al ERP en `:00`.

---

## 7. Hoja de ruta de módulos (el proyecto crece)

> El cotizador es el **primer** módulo. La arquitectura ya deja espacio para los demás.

| Módulo | Descripción | Fase prevista | Estado |
|---|---|---|---|
| **Cotizador** | Motor de costeo, historial, aprobación, PDF | 1–9 | 🟡 En curso |
| **Catálogos** | Productos, activos, servicios, precios, tipos de cambio | 2 | ⬜ |
| **Clientes** | Ficha, historial, contacto contractual | 4 | ⬜ |
| **Calendario de trabajos** | Agenda de visitas/servicios por técnico y fecha | 11 | ⬜ Aparece como "Próximamente" en el Prototipo D |
| **Plantillas de actividades** | Checklists de trabajo por tipo de servicio | 11 | ⬜ Aparece como "Próximamente" en el Prototipo D |
| **Órdenes de trabajo** | Conversión de cotización aprobada → orden → ejecución | 11 | ⬜ |
| **Facturación / Cobranza** | Emisión y seguimiento de facturas | 11 | ⬜ |
| **Reportes gerenciales** | Consolidados y tableros avanzados | 8 | ⬜ |
| **Integración ERP** | Motor ETL: flujos origen→transformación→validación→destino, sincronización por evento y programada | 12 | ⬜ |
| ~~App móvil (técnicos)~~ | ~~Offline, GPS~~ → **PWA web** (decisión D-13) | 10 | ⏸️ Fuera de alcance como proyecto propio |

> El sidebar del **Prototipo D** ya contempla el grupo "Próximamente" con Calendario de trabajos, Plantillas de actividades y Catálogos → mantener esa convención al agregar ítems.

---

## 8. Definición de "Listo" (Definition of Done)

Una tarea se marca ✅ solo si cumple **todo** lo siguiente:

1. Compila sin errores ni advertencias nuevas (`dotnet build` limpio).
2. `dotnet test` en verde.
3. Sigue las convenciones de §4.4 (nombres, `ApiResponse<T>`, permisos, `CancellationToken`).
4. Endpoints con `[Authorize]` + `Permissions.X` y Swagger documentado.
5. Validaciones de negocio con mensaje en **español** claro para el usuario final.
6. Cambios de datos auditables (Fase 7 en adelante).
7. Sin secretos en el código ni en `appsettings.json`.
8. Si toca el motor de cálculo: **tests golden master contra el Excel en verde**.
9. Si toca la UI: verificada en escritorio **y** en móvil (breakpoint 760 px).
10. Documentación actualizada en `docs/` y este README con el estado al día.
11. **Toda corrección a la lógica del Excel queda documentada** con: hallazgo (§2.4) → regla nueva (RN-xx)
    → caso de prueba golden master, y **validada por el cliente antes de marcarse ✅** (decisión D-14).
12. Si toca el modelo de datos: `docs/BASE-DE-DATOS.md` actualizado + migración nueva (nunca
    `EnsureCreated`, nunca editar una migración ya aplicada).

---

## 9. Registro de decisiones (ADR)

| # | Decisión | Fecha | Estado |
|---|---|---|---|
| ADR-001 | Se adopta el **`Cotizador Web — Prototipo D (sidebar + Cotizaciones).html`** como diseño base de la UI | 2026-09-27 | ✅ Aprobada |
| ADR-002 | El login **no** aparece en los prototipos pero **entra en el alcance** (Fase 1) | 2026-09-27 | ✅ Aprobada |
| ADR-003 | La estructura de solución replica `Mikaela.Prestamos` (Clean Architecture, capa de servicios, sin CQRS) | 2026-09-27 | ✅ Aprobada |
| ADR-004 | Patrón de aplicación: **capa de servicios + repositorios**, no MediatR/CQRS | 2026-09-27 | ✅ Aprobada |
| ADR-005 | El motor de cálculo es **código de dominio puro** con tests golden master contra el Excel | 2026-09-27 | ✅ Aprobada |
| **ADR-006** | **Base de datos: SQL Server** (`Microsoft.EntityFrameworkCore.SqlServer`), esquemas `dbo` / `seg` / `int` / `aud` | 2026-09-27 | ✅ **Aprobada (D-01)** |
| **ADR-007** | **Seguridad propia**: ASP.NET Core Identity sobre el esquema `seg` + `JwtBearer` emitido por la propia API. **Sin dependencia de `MerakodeSoft.Auth`** ni de ningún otro proyecto | 2026-09-27 | ✅ **Aprobada (D-02)** |
| **ADR-011** | **Sin proyecto MAUI.** Fuera de alcance. Si se hace, será un proyecto/repositorio **separado**. La Fase 10 queda como responsive web + PWA | 2026-09-27 | ✅ **Aprobada (D-13)** |
| **ADR-012** | El Excel es la fuente de los **datos**, **no** de la lógica defectuosa. Los 12 hallazgos de §2.4 se corrigen, y **cada corrección requiere validación del cliente antes de implementarse** | 2026-09-27 | ✅ **Aprobada (D-14)** |
| **ADR-013** | 4 esquemas SQL Server: `dbo` (negocio), `seg` (seguridad), `int` (integraciones ERP), `aud` (auditoría). Todos los catálogos llevan `OrigenDato` + `CodigoExterno` + `HashContenido` | 2026-09-27 | 🔵 En revisión (`docs/BASE-DE-DATOS.md`) |
| **ADR-014** | Dinero en `decimal(18,6)` **sin redondeo intermedio**; el redondeo a 2 decimales ocurre solo al presentar. Margen en `decimal(5,4)` como fracción con `CHECK 0..1` | 2026-09-27 | 🔵 En revisión |
| **ADR-016** | **.NET 10** (`net10.0`) en los 8 proyectos y **MudBlazor 9.11.0** para toda la UI. Sin proyecto MAUI (ADR-011). Paquetes con versión fijada en §4.1.1 | 2026-09-27 | ✅ Aprobada |
| **ADR-018** | **En el código no hay catálogos.** Ningún `enum` de C# define un conjunto de negocio (estados, modalidades, unidades, niveles): cada conjunto existe en la base como tabla `dbo.*Catalogo`, se edita desde Administración, y la fila de catálogo se resuelve por `Codigo` al arrancar. El formato de una numeración oficial (plantilla, prefijo, ceros) también vive en la tabla (ADR-019) | 2026-09-27 | ✅ Aprobada |
| **ADR-019** | **El formato del consecutivo es configurable, no código.** `TipoConsecutivoCatalogo.Plantilla` es texto libre con las secciones `{PRE}`, `{ANIO}`, `{NUM}`, `{SUF}`; `{NUM}` es obligatoria y cada sección va a lo sumo una vez. Una UDF escalar (`dbo.FnPlantillaConsecutivoValida`) + `CHECK` lo validan en la base. Solo se numera lo que la plataforma crea (hoy: `Cotizacion`); cliente/producto/activo los numera el ERP y no se renumeran (§5.15) | 2026-09-27 | ✅ Aprobada |
| ADR-008 | Estrategia de margen: decimal en fracción (0.055) — corrige H-02 | 2026-09-27 | ⏳ En espera de D-04 |
| ADR-009 | Tipo de cambio: *último valor ≤ fecha* — corrige H-01 | 2026-09-27 | ⏳ En espera de D-06 |
| ADR-010 | Costeo empresarial por semestre independiente — corrige H-03 | 2026-09-27 | ⏳ En espera de D-03 |
| **ADR-015** | La integración con ERP se modela como un **motor ETL**: `int.ErpFlujo` (el "paquete") + `ErpOrigen` (tabla/vista/consulta SQL/API/archivo) + `ErpMapeoCampo` (una fila por campo, con ~20 transformaciones) + `ErpValidacion` + `int.ErpRechazo` (bandeja) + `ErpDestino` (*upsert* por clave y detección por `HashContenido`) + `ErpDependencia` (grafo de orden) + `int.ErpSimulacion` (se ve qué se escribiría **antes** de escribirlo). **Mapeo declarativo: se configura, no se programa.** Frecuencia por defecto **híbrida**: evento (2–5 s) + programado cada 5 min como red de seguridad. El sondeo de 30–60 s queda disponible pero **no es el valor por defecto** | 2026-09-27 | 🟡 Diseño cerrado, pendiente de D-15 |
| **ADR-017** | **La integración con el ERP es de una sola dirección (D-19).** El cotizador solo lee; nunca escribe en el ERP del cliente. Sin `OutboxMensaje`, sin endpoint de escritura, sin método `Escribir` en `IErpConector`. El permiso SQL que se pide al DBA es `db_datareader` y `ErpConexion.EsSoloLectura` tiene `CHECK (= 1)`, de modo que la restricción vive en la base y no en una convención. La orden de compra queda como **diseño diferido** documentado (`BASE-DE-DATOS.md` §7.16), no como tarea comprometida | 2026-09-27 | ✅ Aprobada |

---

## 10. Resumen de avance

| Fase | Nombre | Estado |
|---|---|---|
| 0 | Cimientos e infraestructura | 🟡 En curso (0.2–0.5 ✅, 0.7 ✅) |
| 1 | Autenticación, roles y permisos | ⬜ Pendiente |
| 2 | Catálogos y migración desde el Excel | ⬜ Pendiente |
| 3 | Motor de cálculo | ⬜ Pendiente |
| 4 | Cotizador (API + UI) | ⬜ Pendiente |
| 5 | PDF y envío al cliente | ⬜ Pendiente |
| 6 | Flujo de aprobación | ⬜ Pendiente |
| 7 | Auditoría | ⬜ Pendiente |
| 8 | Dashboard y reportes | ⬜ Pendiente |
| 9 | Endurecimiento, despliegue y migración del Excel | ⬜ Pendiente |
| 10 | Campo: responsive web + PWA | ⬜ Pendiente |
| 11 | Módulos operativos (calendario, órdenes, facturación) | ⬜ Pendiente |
| 12 | 🔌 Integración con ERP del cliente | 🔴 Bloqueada por D-15 |

```
╔══════════════════════════════════════════════════════════╗
║  FASE 0  →  modelo de datos + andamiaje de la solución   ║
║  FASE 1  →  login, roles y permisos (bloquea el resto)  ║
║  FASE 2  →  catálogo + carga de datos del Excel         ║
║  FASE 3  →  motor de cálculo validado vs. Excel  ★      ║
║  FASE 4  →  cotizador completo (Prototipo D)            ║
╚══════════════════════════════════════════════════════════╝
```

---

## 11. Glosario

| Término | Significado |
|---|---|
| **Cotización** | Documento que detalla el precio de un servicio de control de plagas. Número formal `N° 0001`. |
| **Insumo / Producto** | Consumible (ml, gr, L, und, H.H, km, Par…) con costo y precio. |
| **Activo amortizable** | Equipo instalado de forma permanente (lámpara UV, cebadero, trampa) cuyo costo se prorratea por vida útil. |
| **Valor mensual** | `(PrecioUnitario × Cantidad) / MesesVidaÚtil`. |
| **Margen deseado** | Fracción sobre el costo total que define el precio de venta (`0.055` = 5.5 %). |
| **Porcentaje de plantilla** | `CostoTotal / PrecioC$`. Indicador de control interno; no afecta el precio. |
| **Córdoba (C$)** | Moneda local. **Dólar (US$)** = precio en córdobas ÷ tipo de cambio. |
| **Tipo de cambio** | Córdobas por 1 dólar, con histórico diario. |
| **RN** | Regla de negocio del motor de cálculo (§2.3). |
| **H-xx** | Hallazgo/riesgo detectado en el Excel actual (§2.4). |
| **D-xx** | Decisión, confirmada (§3.1) o pendiente (§3.2). |
| **ADR-xxx** | Registro de decisión de arquitectura (§9). |
| **RN-09** | Regla de búsqueda del tipo de cambio: *último valor conocido ≤ fecha*. |
| **OrigenDato** | Marca de cada fila de catálogo: `Manual` (capturada en la web) o `Erp` (sincronizada). |
| **Código externo** | Identificador de la entidad en el ERP del cliente, para poder mapearla de vuelta. |
| **Hash de contenido** | Huella del contenido de la fila del ERP; si no cambió, no se escribe (sincronización eficiente). |
| **Outbox** | Cola de mensajes pendientes de enviar **hacia** el ERP (cotización aprobada → orden/presupuesto). |

---

*Documento mantenido por el equipo de desarrollo. Actualizar al cerrar cada tarea.*
