# ORKIN — Roadmap de migración del legacy `sinco_wos`

> **Estado:** 🟢 Fases 0–9 cerradas (Fase 10 pendiente de instrucción explícita; D-23 pendiente de decisión; D-24 decidida, refactor a códigos ERP pendiente de implementación; sin push) · **Fecha:** 2026-11-18
> **Origen:** `D:\repos\sinco-orkin-lagacy\sinco_wos`
> **Destino:** plataforma web .NET 10 / Blazor WebAssembly / MudBlazor / EF Core 10, con `SINCOERPORKIN` como BD principal.
>
> **Principio rector:** no se migra código, se migran **capacidades**. Se conservan las reglas de negocio válidas y se corrigen los defectos del legacy (SQL concatenado, credenciales incrustadas, estados ambiguos, servicios incompletos). El cotizador es el **módulo 1**; este roadmap cubre el resto de las opciones que hoy solo existen en el WinForms.
>
> **Regla de oro de catálogos (D-20):** **no se copian catálogos operativos del legacy** que ya existen en SOFTLAND/FUMIPLAGA o en las vistas `vv*` de `SINCOERPORKIN` (frecuencia, locación, plaga, ubicación, dosificación, método, equipo, vehículo, bodega): se leen en vivo y solo se guarda el código ERP. Los **maestros de cotización** (cliente, producto, unidad, categoría, tipo de servicio, tipo de cambio, técnico y activo amortizable) sí se materializan una sola vez en `dbo` porque el cotizador los usa con `Id` propio, auditoría y CRUD; no se crea una tabla paralela "por si acaso". Ver D-24 (§8).
>
> **Regla de oro de lo legacy (D-22):** para las capacidades del WinForms **se usan las estructuras de BD que ya existen** (`Plantilla_Orden_Trabajo`, `_Detalle`, `_Servicio`, `_Equipos`, `Plantilla_Calendario`, `TmpPlantillas`). Lo que se migra es **cómo se gestionan y se visualizan** (API + frontend), no su esquema. No se crean tablas nuevas ni se importan datos a tablas paralelas.

---

## 1. Qué es hoy el legacy

| Aspecto | Legacy | Destino |
|---|---|---|
| UI | WinForms, .NET Framework 4.7.2 | Blazor WebAssembly + MudBlazor 9 |
| Datos | EF6 Database First (EDMX) + `SqlCommand` con SQL concatenado | EF Core 10, LINQ parametrizado + repositorios |
| BD | `sincoerporkin`, esquema `dbo`, prefijo dinámico `basedatos.compania.TABLA` | `SINCOERPORKIN`, esquemas `dbo`, `seg`, `int`, `aud` |
| Seguridad | `Seguridad_Usuarios`, `Seguridad_Roles`, `Seguridad_Usuarios_Permisos`; login contra `vvUsuarios` | ASP.NET Core Identity + JWT + permisos por claims |
| Reportes | `Microsoft.Reporting.WinForms` + `OrdenesDeTrabajo.rdlc` + SPs `SINCO_IMPRIMIR_*` | Servicio de reportes en la API + visor/PDF en la web |
| Integraciones | Google Calendar API (OAuth) + SPs de Exactus + SPs de correo | Agenda propia + adaptadores + `BackgroundService` |
| Procesos | Consola programada externamente + Windows Service incompleto | `IHostedService` / job de la API, con auditoría y reintentos |
| Dependencias | 26 tablas, 16 vistas `vv*`, 29 stored procedures | Modelo propio, vistas `vv*` como origen ETL, SPs envueltos y luego retirados |

**Módulos funcionales del legacy (menú principal de `Frmprincipal`):**

`CATALOGOS` · `PLANTILLAS` · `PRICING` *(inerte)* · `PROGRAMACIÓN DE VISITAS` · `ORDENES DE TRABAJO` · `REQUISAS GENERADAS` · `DOCTOS DE INVENTARIO` · `REPORTES` *(inerte)* · `SALIR` · `CAMBIAR CONTRASEÑA` *(inerte)*.

---

## 2. Mapa de opciones → destino

Leyenda de estado: ✅ ya cubierto · 🔶 parcial · 🆕 por construir · ❓ requiere decisión.

| # | Opción legacy | Formulario legacy | Destino en la plataforma | Estado | Fase |
|---|---|---|---|---|---|
| 1 | Login / cambiar servidor | `Frmlogin`, `Frmsettings` | `AuthController` + `/login`, JWT, "Recordarme", `/cambiar-contrasena` | ✅ | 0 |
| 2 | Menú principal | `Frmprincipal` | `MainLayout.razor` (sidebar MudBlazor por permisos; `PRICING` retirado) | ✅ | 0/9 |
| 3 | Catálogo de artículos | `FrmPlantillaCatalogo` | `/catalogos/productos` + ETL desde `vvArticulos` | ✅ | 0/1 |
| 4 | Catálogo de dosificación | `FrmPlantillaCatalogo` | `vvDosificacion` se consulta desde `api/catalogos/opciones-erp`; **no se copia** y el editor muestra descripción + código | ✅ | 2 |
| 5 | Catálogo de frecuencia | `FrmPlantillaCatalogo` | `vvFrecuencia` se consulta desde `api/catalogos/opciones-erp`; **no se copia** | ✅ | 2 |
| 6 | Catálogo de técnicos | `FrmPlantillaCatalogo` | `/catalogos/tecnicos` + ETL `vvTecnicos` — sí se conserva: lo usa el cotizador | ✅ | 1 |
| 7 | Catálogo de servicios | `FrmPlantillaCatalogo` | `/catalogos/tipos-servicio` | ✅ | 0 |
| 8 | Catálogo de vehículos | `FrmPlantillaCatalogo` | `vvVehiculos` se consulta desde `api/catalogos/opciones-erp`; **no se copia** | ✅ | 2 |
| 9 | Catálogo de usuarios | `FrmPlantillaCatalogo` | `/administracion/usuarios` (seg.Usuario) | 🔶 | 1/10 |
| 10 | Parámetros generales | `FormParametroGenerales` | `/administracion/parametros` | ✅ | 0 |
| 11 | Tipos de plaga / ubicación / método de aplicación (catálogos auxiliares) | Modo selección | `vvTipodePlagas`, `vvTipodeUbicaciones` y `vvMetodosAplicacion` se consultan desde `api/catalogos/opciones-erp`; **no se copian** | ✅ | 2 |
| 12 | Locaciones de cliente | Modo selección | `vvLocacionesClientes` se consulta filtrando por cliente desde `api/catalogos/opciones-erp`; **no se copia** | ✅ | 2 |
| 13 | Bodegas | Modo selección | Ya existe en `vvBodegas`; **no se copia** | ⛔ D-20 | — |
| 14 | Tipos de equipo | Modo selección | `vvTipodeEquipos` se consulta desde `api/catalogos/opciones-erp`; **no se copia** | ✅ | 2 |
| 15 | Plantillas (CRUD + copiar) | `FrmPlantillas`, `FrmCrudPlantillas` | `/plantillas` (alta, edición y copia en diálogo con pestañas; códigos ERP ahora se eligen en combos legibles y se validan contra las `vv*`) | ✅ | 2 |
| 16 | Programación de visitas | `FrmProgramacionVisitas` | `/visitas` (calendario mensual, semana × técnico, lista y filtros) | ✅ | 3 |
| 17 | Confirmar visita / asignar técnicos y plantillas | `FrmAsociacionTecnicoVisitaConfirmada` | Diálogo de visita en `/visitas` con técnicos y plantilla | ✅ | 3 |
| 18 | Ver / reversar visitas confirmadas | `FormProgramacionVisitasConfirmadas` | Panel lateral de detalle y acción `Reversar` en `/visitas` | ✅ | 3 |
| 19 | Generar orden de trabajo desde visita | `SINCO_GENERA_ORDENE_DE_TRABAJO` | Acción `Generar OT` en `/visitas` (SP envuelto por `IGeneradorOrdenTrabajo`) | ✅ | 3 |
| 20 | Órdenes de trabajo (listado + ABM + anular) | `FrmPlantillas`, `FrmCrudOrdenes` | `/ordenes` + `/ordenes/nueva|editar|detalle` | 🆕 | 4 |
| 21 | Detalle OT: productos / servicios / equipos / técnicos | Pestañas de `FrmCrudOrdenes` | Un solo formulario con pestañas MudBlazor | 🆕 | 4 |
| 22 | Generar requisa desde OT | `SINCO_GENERA_REQUISA` | Acción en detalle de OT | 🆕 | 4/5 |
| 23 | Requisas generadas (listado + ABM + anular) | `FrmPlantillas`, `FrmCrudRequisas` | `/requisas` + `/requisas/nueva|editar|detalle` | 🆕 | 5 |
| 24 | Detalle requisa: entregado / utilizado / devuelto / SobFal | `FormDetalleRequisa` | Componente de línea con cálculo de SobFal | 🆕 | 5 |
| 25 | Documentos de inventario + detalle | `FrmDocumentoInventario` | `/inventario/documentos` | ✅ | 6 |
| 26 | Generar / trasladar documento a Exactus | SPs `SINCO_GENERA_DOCINV_EXACTUS`, `SINCO_TRASLADAR_DOCINV_EXACTUS` | Servicio `IExactusService` envolviendo los SPs con parámetros | ✅ | 6 |
| 27 | Imprimir orden de trabajo | `FrmReportViewer` + `OrdenesDeTrabajo.rdlc` | Acción `Imprimir` en `/ordenes` → PDF generado en la API desde `SINCO_IMPRIMIR_ORDENE_DE_TRABAJO` | ✅ | 7 |
| 28 | Reportes de requisas y costos | SPs `SINCO_IMPRIMIR_REQUISAS*`, `SINCO_CONSULTAR_*` | `/reportes` — visor, PDF y CSV desde API sobre los datasets existentes | ✅ | 7 |
| 29 | Notificaciones de visitas y correos | Consola + Windows Service + SPs `SINCO_NOTIFICAR_*` | `IHostedService` + proveedor SMTP configurable, bandeja y campana | ✅ | 8 |
| 30 | Integración Google Calendar | `GoogleCalendar.cs` | **Agenda propia con calendarios por técnico y su color + importador** de eventos existentes | ✅ decisión cerrada | 3 |
| 31 | `PRICING` | Sin handler | **Retirado del menú**; estructura preparada para reactivarlo | ✅ decisión cerrada | 9 |
| 32 | `REPORTES` (botón del menú) | Sin handler | `/reportes` con catálogo único de 13 SPs y permisos `REPORTES.*` | ✅ | 7 |
| 33 | `CAMBIAR CONTRASEÑA` (label) | Sin handler | Ya existe en `/cambiar-contrasena` | ✅ | 0 |
| 34 | Campana de notificaciones | Decorativa | Campana en el sidebar con badge y sondeo de bandeja | ✅ | 8 |

---

## 3. Estrategia de datos

### 3.1 Tablas del legacy (26)

| Grupo | Tablas legacy | Acción |
|---|---|---|
| Ya cubiertas por el cotizador | `ParametrosGenerales`, `Seguridad_*` (parcial) | Mapear a `dbo.ParametroGeneral`, `seg.Usuario`, `seg.Rol`, `seg.Permiso` |
| Catálogos a crear/confirmar | Maestros `U_*` vía vistas `vv*` | ETL con homologación + simulación (patrón yaImplementado) |
| Núcleo operativo legacy (D-22) | `Plantilla_Orden_Trabajo`, `_Detalle`, `_Servicio`, `_Equipos` | **Tablas existentes, mapeadas tal cual** con `ExcludeFromMigrations()`. Clave `PLANTILLA` (`bigint`, sin identity), hijos con `int`, `ACTIVO = SI/NO`, consecutivos `MAX + 1`. No se crean tablas ni se importan datos. |
| Núcleo operativo nuevo | `Orden_Trabajo*`, `Requisa*`, `ProgramacionVisita*`, `Documento_Inv*` | Modelo EF Core propio sobre las estructuras del ERP, con `Codigo`/`Numero` consecutivo y auditoría |
| Técnicas/puente | `NotificarProgramacionVisitaTemporal`, `NotificacionEnviada`, `logs` | ✅ Fase 8: `dbo.NotificacionEnviada` (singular, tabla existente) se extiende con `CuerpoHtml`, `ClaveNegocio`, `ProximoIntentoUtc` y `LeidoUtc`; `int.JobEjecucion` registra cada corrida. No se crea `aud.Notificacion` ni tabla paralela de correos. |
| Núcleo operativo legacy (D-22) | `Plantilla_Calendario` | **Tabla existente**, mapeada tal cual: la agenda propia la usa para alta, edición y reversa. No se crean tablas de visitas paralelas. |

### 3.2 Stored procedures (29)

No se migran "como están". Se clasifican y envuelven:

| Categoría | SPs | Tratamiento |
|---|---|---|
| Generación de documentos | `SINCO_GENERA_ORDENE_DE_TRABAJO`, `SINCO_GENERA_ORDENES_DE_TRABAJO`, `SINCO_GENERA_REQUISA`, `SINCO_GENERA_DOCINV_EXACTUS`, `SINCO_TRASLADAR_DOCINV_EXACTUS` | Fases 4–6: envolver en servicios con parámetros; Fase 10: trasladar regla a C# y dejar SP de respaldo |
| Anulación / renumeración | `SINCO_ANULAR_ORDENES_DE_TRABAJO`, `SINCO_ELIMINAR_NUMERO_DE_ORDEN`, `SINCO_CAMBIAR_NUMERO_DE_ORDEN`, `SINCO_ELIMINAR_REQUISA` | Convertir a servicios con transacción y auditoría (nunca borrado físico sin confirmación) |
| Reportes | `SINCO_IMPRIMIR_ORDENE_DE_TRABAJO(N)`, `SINCO_IMPRIMIR_REQUISAS*`, `SINCO_IMPRIMIR_ORDENES_DE_TRABAJO` | ✅ Fase 7: `ServicioReportes` los invoca con parámetros y `GeneradorPdfReporte` arma el PDF en la API |
| Notificaciones | `SINCO_NOTIFICAR_CLIENTES`, `SINCO_NOTIFICAR_PROXIMOS_SERVICIOS`, `SINCO_NOTIFICAR_CORREOS_ENVIADOS`, `SINCO_NOTIFICAR_REQUISAS_ERRADAS`, `SINCO_MOVER_NOTIFICACION_DESERVICIOS` | ✅ Fase 8: `NotificacionBackgroundService` + proveedor SMTP. Los SPs se invocan solo como fuentes de lectura parametrizada (`ILectorSql`, SQL fijo y `SqlParameter`), previa validación de `OBJECT_DEFINITION` y permiso `EXECUTE`. `SINCO_MOVER_NOTIFICACION_DESERVICIOS` queda fuera: muta el staging. |
| Consultas de costos/cartera | `SINCO_CONSULTAR_*`, `SINCO_SP_CARTERA_ANTIGUEDAD` | ✅ Fase 7: proyectadas como endpoints de `/reportes` con PDF y CSV (la cartera depende de `FUMIPLAGA.FMP`) |
| Utilitarios | `SINCO_LIMPIAR_TABLAS_DE_TRABAJO`, `SINCO_ACTUALIZAR_DATOSLOCACIONES` | No migrar; sustituir por mantenimiento web |

> **Regla de oro:** ningún SP se invoca con SQL concatenado. Todos pasan por `SqlParameter` o por `FromSqlInterpolated`, con auditoría de quién lo ejecutó.

### 3.3 Vistas `vv*`

Las vistas `vv*` tienen dos papeles distintos: son el **origen ETL de los maestros de cotización** (`vvArticulos`, `vvClientes`, `vvTecnicos`, `vvTipoServicios`) y la **fuente de solo lectura de los códigos operativos** (`vvFrecuencia`, `vvLocacionesClientes`, `vvBodegas`, etc.), que no generan tabla destino (D-20). Se respeta el patrón actual: homologación en tabla, mapeo visible en la grilla del flujo, simulación antes de ejecutar y una corrida activa por (conexión, catálogo). Ver D-24 (§8).

### 3.4 Catálogo único (D-20 / D-21)

La plataforma es **una sola aplicación** para el cotizador y para las capacidades del legacy. Por lo tanto hay **un solo conjunto de catálogos**, no uno por módulo:

```
SOFTLAND / FUMIPLAGA  →  vv* de SINCOERPORKIN  →  ETL con homologación  →  catálogos de la plataforma
                                                                              ├─ Cotizador
                                                                              └─ Módulos operativos (OT, visitas, requisas, inventario)
```

- **Un Solo origen:** todo catálogo termina viniendo de Softland. Si ya existe allí o en `SINCOERPORKIN.vv*`, **no se crea otra tabla ni otro cargador**: se consume desde ese origen.
- **Una sola copia local:** los catálogos que el cotizador necesita tener en la plataforma (productos, clientes, unidades, categorías, tipos de servicio, tipos de cambio, técnicos) viven una sola vez en `dbo` y son usados por todos los módulos.
- **Una sola API:** un mismo endpoint por entidad, con un mismo `Id`. No habrá `/catalogos/tecnicos/cotizador` y `/catalogos/tecnicos/ordenes`.
- **Mantenimiento manual solo donde aplique:** se permite corregir precios o datos propios del cotizador; nunca se "mantienen" manualmente catálogos que son propiedad de Softland.
- **Ejemplo resuelto:** frecuencias, locaciones, métodos de aplicación, tipos de plaga, tipos de ubicación y vehículos se retiraron del código porque ya están en el ERP. Cuando las órdenes de trabajo o las visitas los necesiten, leerán el mismo catálogo único, sin tablas nuevas.

### 3.5 Menú unificado

El sidebar es de la **plataforma**, no del cotizador:

| Grupo | Contenido | Estado |
|---|---|---|
| `Dashboard` | KPIs de toda la operación | ✅ |
| `Cotizaciones` | Módulo 1 | ✅ |
| `Catálogos` | Clientes, productos, unidades, categorías, tipos de servicio, tipos de cambio y técnicos. Las conexiones/orígenes ETL se muestran aparte, con permiso propio. | ✅ |
| `Operación` | Plantillas, visitas, órdenes, requisas, inventario, reportes | ✅ Plantillas (Fase 2), visitas (Fase 3), órdenes (Fase 4), requisas (Fase 5), inventario (Fase 6) y reportes (Fase 7) |
| `Administración` | Usuarios, roles, parámetros, bitácora, cambiar contraseña | ✅ |

`PRICING` no aparece. `REPORTES` está dentro de `Operación` (Fase 7), no como botón suelto. Ningún grupo muestra dos veces el mismo catálogo.

---

## 4. Fases de migración

### Fase 0 — Fundación ✅ completada
**Objetivo:** dejar cerrada la base sobre la que se montan los demás módulos.

- ✅ API + web contra `SINCOERPORKIN`, health, login JWT, "Recordarme", 401 → login.
- ✅ Cotizador end-to-end, motor de cálculo, catálogos ERP, tipos de cambio históricos.
- ✅ ETL con conexiones, flujos, homologación, simulación y corrida manual.
- ✅ Catálogos base y `U_*`: entidades, cargadores, DTOs y controllers.
- ⛔ **Cancelado por D-20:** la migración `AgregarCatalogosLegacy` y los catálogos `Frecuencia`, `Locacion`, `MetodoAplicacion`, `TipoPlaga`, `TipoUbicacion` y `Vehiculo` no se aplican ni se cargan: ya existen en `SINCOERPORKIN.vv*` / SOFTLAND y el cotizador no los necesita. Se retiran del código y del alcance (ver §6).
- ✅ Catálogo compartido de técnicos: CRUD web (`/catalogos/tecnicos`), API única (`/api/catalogos/tecnicos`), flujo ETL `80ae2da2-…` y `Cotizacion.TecnicoAsignadoId` apuntando a `dbo.Tecnico` (D-21).
- 🔶 Pendiente real: pantallas UI de los catálogos restantes que **sí** están en el cotizador (unidades, categorías, tipos de servicio, productos, clientes, tipos de cambio).

**Criterio de salida:** catálogos sin duplicados ni datos de prueba; cada flujo con simulación; CRUD manual disponible solo donde haya tabla propia justificada.

---

### Fase 1 — Catálogos y administración legacy
**Objetivo:** que el usuario pueda consultar y mantener desde la web solo lo que el cotizador necesita y que no esté ya resuelto en el ERP.

**Entregables**
- **No crear tablas ni flujos ETL** para catálogos que ya existen en SOFTLAND/FUMIPLAGA o en `SINCOERPORKIN.vv*`: dosificación, frecuencias, locaciones, métodos de aplicación, tipos de plaga, tipos de ubicación, vehículos, tipos de equipo y bodegas. Se consumen desde su origen cuando un módulo operativo lo pida, con homologación configurable.
- ✅ Catálogo de **técnicos** terminado: flujo ETL (`80ae2da2-…`), CRUD web y `Cotizacion.TecnicoAsignadoId` apunta a `dbo.Tecnico`, no a `seg.Usuario`.
- Completar la UI de administración de los catálogos propios: unidades, categorías, tipos de servicio, productos, clientes, tipos de cambio y técnicos.
- Permisos: `CATALOGOS.VER`, `CATALOGOS.EDITAR`, `CATALOGOS.ORIGENES`.

**Criterio de salida:** cero tablas duplicadas de catálogos ERP; cero registros `PRUEBA/TEST/DEMO`; cada catálogo propio con alta, edición, baja lógica y carga ERP con simulación.

---

### Fase 2 — Plantillas ✅ Completada (corregida por D-22)
**Objetivo:** reemplazar la **gestión y visualización** de `FrmPlantillas` / `FrmCrudPlantillas`, sin tocar el esquema de sus tablas.

> ⚠️ **Corrección aplicada:** la primera implementación creó `dbo.Plantilla`, `PlantillaDetalle`, `PlantillaServicio` y `PlantillaEquipo`. Eso **no era válido**: D-22 exige operar sobre las tablas legacy que ya están en `SINCOERPORKIN`. La migración `20261118000037_PlantillasSoloLegacy` eliminó esas cuatro tablas nuevas (estaban vacías) y las entidades/API/UI se reescribieron sobre `Plantilla_Orden_Trabajo*`. Las tablas legacy se mapean con `ExcludeFromMigrations()`: EF las lee y escribe, pero nunca las crea ni las altera.

**Entregables**
- ✅ Entidades `PlantillaOrdenTrabajo`, `PlantillaOrdenTrabajoDetalle`, `PlantillaOrdenTrabajoServicio` y `PlantillaOrdenTrabajoEquipos`, mapeadas a las tablas reales con sus claves compuestas y columnas `varchar`/`decimal` originales. Clave `PLANTILLA` (`bigint`, sin identity) asignada por `MAX + 1`; auditoría `FECHA_CREACION` / `USUARIO_CREACION`; baja lógica `ACTIVO = NO` (no borrado físico, para no romper `Plantilla_Calendario`).
- ✅ Listado con filtros (texto, cliente, solo activas), alta, edición, baja y **Copiar plantilla**, en `/plantillas` con diálogo de pestañas. El alta exige cliente, locación, producto, servicio y equipo; `TECNICO` y `VEHICULO` quedan vacíos como en el WinForms. `COSTO_TOTAL = CANTIDAD × COSTO_UNITARIO`; `UMEDIDA` se conserva tal cual (sin normalizar).
- ✅ Detalle de producto con tipo de plaga, lugar a tratar, dosificación, concentración, método, cantidad y `MAXIMO_PERMITIDO`. La copia conserva siempre `MAXIMO_PERMITIDO` (corrige el defecto del copiador legacy, que lo omitía). Los catálogos ERP se capturan como código texto (D-20), sin tablas paralelas ni FK locales.
- ✅ Permisos sembrados en BD: `PLANTILLAS.VER` (Administrador, Supervisor, Vendedor), `.EDITAR`, `.COPIAR` y `.ELIMINAR` (Administrador, Supervisor).
- ✅ Verificado: build `0 Errores`, tests `88/88`, migración aplicada (0 tablas nuevas; `PlantillasSoloLegacy` es la última en `__EFMigrationsHistory`) y API end-to-end contra datos reales: `soloActivas=true` → **3.218**, `soloActivas=false` → **3.503**, detalle de la plantilla 1 con 4 productos / 1 servicio / 1 equipo y nombres de cliente de `dbo.Cliente`. Sin registros `PRUEBA/TEST/DEMO`.

**Dependencias:** Fase 1 (catálogos únicos). **Riesgo:** medio — resuelto respetando las claves compuestas y los códigos ERP del legacy, sin migración de datos ni reconciliación por `Id` artificial.

---

### Fase 3 — Programación de visitas ✅ Completada
**Objetivo:** traer `FrmProgramacionVisitas` y sus dos formularios auxiliares.

**Decisión cerrada:** fuente de verdad = **agenda propia**, con una interfaz deliberadamente similar a Google Calendar para reducir el impacto del cambio, y un **importador** que migra los eventos que ya tienen los usuarios. OAuth queda confinado al importador inicial; después de migrar, la operación diaria no depende de Google.

> **Aclaración del usuario:** *"no recuerdo bien la interfaz del calendario que usan pero creo que para cada técnico le crean calendario y cada uno con su color correspondiente, quizás algo así podemos hacer o ver si podemos dar algún plus de mejora en la organización"*. Se adoptó como mejora de organización: **un calendario por técnico, con color propio**, activable/desactivable como en Google Calendar.

**Entregables**
- ✅ **Sin tablas paralelas de visitas (D-22).** La agenda opera sobre `dbo.ProgramacionVisitaConfirmadas`, `dbo.ProgramacionVisitaDetalleConfirmada`, `dbo.Plantilla_Calendario` y `dbo.Orden_Trabajo`, mapeadas con `ExcludeFromMigrations()` (claves `ID_VISITA`, `(ID_VISITA, LINEA)`, `(PLANTILLA, ID_CALENDARIO)`). Consecutivo `MAX + 1`.
- ✅ **UI `/visitas`**: vista mensual con chips por visita, vista **Semana × técnico** (una fila por técnico; una visita compartida aparece en cada técnico asignado), panel lateral de detalle, filtros por texto/cliente/plantilla/rango, `+` por día, alta/edición en diálogo, reversa y generación de OT. Implementada en `Pages/Visitas.razor` + `VisitaDialogo.razor`, `VisitaModels.cs` y `VisitaService.cs` del cliente.
- ✅ **Calendarios por técnico**: el color vive en `dbo.Tecnico.ColorCalendario` (`varchar(7)`, migración `20261118014750_ColorCalendarioTecnico`), el **catálogo compartido único** (D-21), no en una tabla nueva ni en JSON. Se edita en `/catalogos/tecnicos` (campo texto + selector de color) y se valida `#RRGGBB` en `TecnicoService`. Si está vacío, `ColorCalendario.Para(codigo)` deriva uno determinístico de una paleta que empieza por el rojo corporativo `#E31837`. El panel "Calendarios" permite activar/desactivar cada técnico y la grilla respeta el filtro.
- ✅ **Importador de Google Calendar**: `ImportadorGoogleService` reutiliza el parseo legacy de `Summary`/`Ubicacion`/`Descripcion`, valida cliente/locación/servicio/vehículo/técnico contra las vistas `vv*` (`ACTIVO='S'` cliente, `ACTIVO='SI'` resto), homologa con diálogo de corrección y confirma la importación desde `ImportadorGoogleCalendar.razor`.
- ✅ **Enriquecimiento permitido**: el importador sí agrega estructura nueva, porque es auditoría y staging que no existía: `int.ImportacionGoogleLote`, `int.ImportacionGoogleEvento` e `int.ErpMapeoEventoGoogle` (migración `20261118012934_ImportadorGoogleCalendar`). No duplica catálogos ni visitas.
- ✅ **OT desde visita**: `IGeneradorOrdenTrabajo` envuelve `dbo.SINCO_GENERA_ORDENE_DE_TRABAJO` con `@Id_Visita`. La reversa borra orden detalle → visita → `Plantilla_Calendario` y se bloquea si la visita ya tiene OT.
- ✅ **Permisos sembrados** `VISITAS.VER`, `.EDITAR`, `.REVERSAR`, `.GENERAR_OT`, `.IMPORTAR`.
- ✅ **Verificado**: build `0 Errores`, tests `100/100` (`Orkin.Cotizador.Tests/Visitas`), `dotnet ef migrations has-pending-model-changes` → *"No changes have been made to the model since the last migration"*.

**Dependencias:** Fase 2. **Riesgo:** alto — resuelto respetando las claves compuestas del legacy y concentrando el color en el catálogo compartido. Ver **Anexo A**.

---

### Fase 4 — Órdenes de trabajo ✅ Completada
**Objetivo:** reemplazar `FrmCrudOrdenes` y `FrmPlantillas` en modo `OrdenTrabajo`.

**Entregables**
- ✅ **Cero tablas paralelas (D-22).** Cinco entidades sobre `dbo.Orden_Trabajo`, `_Detalle`, `_Servicio`, `_Equipos` y `_Tecnicos`, con `ExcludeFromMigrations()`. La clave expuesta por API y UI es la PK real `(ORDEN_NUMERO, ID_VISITA, PLANTILLA)`: no se inventa un `Id` (D-21). Se respeta la inconsistencia histórica de tipo de `PLANTILLA` (`bigint` en cabecera/servicios/técnicos, `int` en detalle/equipos).
- ✅ **UI `/ordenes`**: KPI de GE/AP/AN, grilla con filtros por texto, cliente, estado y rango de fechas; conteos de hijas por rango de orden y agrupación en memoria para no disparar el `OPENJSON('$')` que falla en el SQL Server del ERP. Acciones Editar/Anular/Requisa con `ConfirmarDialogo` y mensajes legacy.
- ✅ **Alta desde visita confirmada** (`NuevaOrdenDialogo`): reutiliza `IGeneradorOrdenTrabajo` y `dbo.SINCO_GENERA_ORDENE_DE_TRABAJO`. No se reimplementó la rama manual del WinForms.
- ✅ **Edición con pestañas** (`OrdenDialogo`): Productos, Servicios, Equipos y Técnicos con combos legibles desde la API única de catálogos (D-20/D-21); costo real desde `COSTO_PROM_LOC`, unidad desde `UNIDAD_VENTA` y precio unitario ajustable. Solo se permite en `GE`.
- ✅ **Estados y ciclo de vida**: `GE` generada, `AP` aprobada, `AN` anulada. `AP` solo se alcanza hoy al generar la requisa con `dbo.SINCO_GENERA_REQUISA`; no existe SP de aprobación separado. La anulación usa `dbo.SINCO_ANULAR_ORDENES_DE_TRABAJO` y también anula la requisa. Los mensajes `RAISERROR` del legacy se reenvían tal cual.
- ✅ **Permisos sembrados** `OT.VER`, `.EDITAR`, `.ANULAR`, `.APROBAR`, `.IMPRIMIR` en `seg.Permiso`, `dbo.ModuloPermisoCatalogo` y los tres roles; link `Órdenes de trabajo` en `Operación`.
- ✅ Imprimir (Fase 7): acción en la grilla con `OT.IMPRIMIR`, tooltip que advierte que solo se imprimen órdenes `AP` y mensaje del SP reenviado sin traducir.
- ✅ **Verificado**: build `0 Errores`, tests `108/108` (7 pruebas nuevas de Fase 4 sobre proveedor InMemory, sin mutar datos reales).

**Dependencias:** Fases 2 y 3. **Riesgo:** alto — resuelto delegando el ciclo de vida a los SPs originales y conservando sus mensajes y consecutivos.

---

### Fase 5 — Requisas ✅ Completada
**Objetivo:** reemplazar `FrmCrudRequisas` y `FormDetalleRequisa`.

**Entregables**
- ✅ **Cero tablas paralelas (D-22).** Tres entidades sobre `dbo.Requisa`, `dbo.Requisa_Detalle` y `dbo.Requisa_Tecnicos` con `ExcludeFromMigrations()`. La clave expuesta es `REQUISA_NUMERO`, el mismo número que la orden legacy (D-21). Bodegas leídas de `dbo.vvBodegas`, sin copiar el catálogo (D-20).
- ✅ **UI `/requisas`**: KPIs de GE/AP/AN sobre el filtro, grilla con número de requisa/orden, cliente y locación con descripción real, bodega, fechas, estado, conteo de líneas/técnicos e instrucciones; filtros por texto, cliente, estado y rango de fechas. Conteos y nombres resueltos por rango y agrupación en memoria, sin `OPENJSON('$')`.
- ✅ **Alta "Nueva desde orden"** (`NuevaRequisaDialogo`): solo órdenes `GE`; delega en `OrdenTrabajoService.GenerarRequisaAsync`, que invoca `dbo.SINCO_GENERA_REQUISA`. No se vuelve a ejecutar el SP desde el listado ni se duplica su lógica (la requisa toma el número de la orden).
- ✅ **Edición con pestañas** (`RequisaDialogo`): cabecera, líneas y técnicos. Selector de producto con descripción, unidad y `COSTO_PROM_LOC`; precio manual permitido. Entregada / utilizada / devuelta y `CANTIDAD_SOBFAL = CANTIDAD_UTILIZADA + CANTIDAD_DEVUELTA - CANTIDAD_ENTREGADA`; `COSTO_TOTAL = CANTIDAD_UTILIZADA * COSTO_UNITARIO`. Se conserva y documenta el signo heredado de SobFal y el bug del mensaje de "cantidad devuelta" que valida `nCantidadUtilizada`. Solo se edita en `GE`; en `AP`/`AN` se devuelve el mensaje exacto del WinForms.
- ✅ **Anulación**: reutiliza `dbo.SINCO_ANULAR_ORDENES_DE_TRABAJO` desde la orden relacionada; no hay SP ni tabla nueva. Mensajes `RAISERROR` y validaciones del legacy se reenvían tal cual.
- ✅ **Permisos sembrados** `REQUISAS.VER`, `.EDITAR`, `.ANULAR`, `.IMPRIMIR` en `seg.Permiso`, `dbo.ModuloPermisoCatalogo` y los tres roles; link `Requisas` en `Operación`. Imprimir se activó en la Fase 7 con `SINCO_IMPRIMIR_REQUISAS` y su tooltip.
- ✅ **Verificado**: build `0 Errores`, tests `115/115` (7 pruebas nuevas de Fase 5 sobre proveedor InMemory, sin mutar datos reales). API real: `GET /api/requisas?estado=GE` devuelve `0048057` (3 líneas, 1 técnico) y `GET /api/catalogos/opciones-erp` devuelve 12 bodegas reales. UI real: menú, KPIs, grilla, detalle de `0048057`, tooltip de impresión y consola sin errores.

**Dependencias:** Fase 4. **Riesgo:** medio — resuelto reutilizando los SPs originales y las vistas reales, y conservando mensajes y reglas heredadas.

---

### Fase 6 — Documentos de inventario y Exactus ✅ Completada (corregida por D-22)
**Objetivo:** reemplazar `FrmDocumentoInventario` y el flujo de exportación.

**Entregables**
- ✅ **Cero tablas paralelas (D-22).** Dos entidades sobre `dbo.Documento_Inv` y `dbo.Documento_Inv_linea` con `ExcludeFromMigrations()`; claves legacy `(PAQUETE_INVENTARIO, DOCUMENTO_INV)` y `(PAQUETE_INVENTARIO, DOCUMENTO_INV, LINEA_DOC_INV)` (D-21). Las líneas las produce el SP, nunca el contexto. Descripciones de artículos desde `dbo.vvArticulos` y de bodegas desde `dbo.vvBodegas`, sin copiar catálogos (D-20).
- ✅ **UI `/inventario/documentos`**: KPIs del filtro (en el filtro / SI / NO / líneas), grilla con documento, requisa u orden, referencia, fecha, bodega con descripción, estado de traslado, líneas y usuario; filtros por texto, transferido `Todos/SI/NO` y rango de fechas. Conteos y nombres por rango y agrupación en memoria, sin `OPENJSON('$')`.
- ✅ **Detalle por artículo** (`DocumentoInventarioDialogo`): descripción real, unidad, bodega y bodega destino con nombre, cantidades y costos. `COSTO_UNITARIO` se calcula como `COSTO_TOTAL_LOCAL / CANTIDAD`, igual que el WinForms; se documenta el quirk heredado del SP de traslado (unitario de origen copiado en `COSTO_TOTAL_LOCAL` de Exactus).
- ✅ **`IExactusService`**: envuelve `dbo.SINCO_GENERA_DOCINV_EXACTUS(@Requisa_Numero)` y `dbo.SINCO_TRASLADAR_DOCINV_EXACTUS(@Documento_Inventario)` con parámetros, sin concatenación SQL; captura el `SELECT ... AS RESULTADO` que el WinForms descartaba, registra usuario/clave/resultado con `ILogger` y reenvía los mensajes `RAISERROR` y de éxito exactos (incluida la confirmación textual del traslado irreversible).
- ✅ **Permisos sembrados** `INVENTARIO.VER`, `.EDITAR`, `.TRANSFERIR` en `seg.Permiso`, `dbo.ModuloPermisoCatalogo` y los tres roles; link `Documentos de inventario` en `Operación`.
- ✅ **Verificado**: build `0 Errores`; tests `122/122` (7 pruebas nuevas de Fase 6 sobre proveedor InMemory con `ExactusFalso`; los SPs no se ejecutaron). API real: filtro `transferido=NO` y detalle de `CN-0025666` (requisa `0026433`) contra `SINCOERPORKIN`, sin mutar documentos. UI real: menú, KPIs, grilla, filtro `Todos/SI/NO`, detalle, tooltips y consola sin errores.

**Dependencias:** Fase 5. **Riesgo:** alto — resuelto envolviendo los SPs actuales con parámetros, registrando su resultado y conservando sus mensajes; no se reimplementó ninguna regla de Exactus.

---

### Fase 7 — Reportes e impresión ✅ Completada
**Objetivo:** activar el botón `REPORTES` hoy inerte y dar salida PDF a OT y requisas.

**Decisión cerrada:** se reutilizan los datasets y los SPs `SINCO_IMPRIMIR_*` actuales, y el **PDF se genera desde la API**; la web solo muestra el visor y ofrece la descarga. No se migra `ReportViewer` al cliente.

**Entregables**
- ✅ `IReporteService`/`ServicioReportes` en Application: catálogo estático de **13 reportes**, un SP por reporte; los nombres de SP y de parámetro salen del catálogo y los valores viajan siempre como `SqlParameter` (sin concatenar datos del usuario). `ILectorSql` permite probarlo sin base de datos.
- ✅ `GeneradorPdfReporte` en la API (QuestPDF 2026.9.1): mismo dataset del SP, cabecera corporativa, tabla y bloque de firmas leído de `vvTipodeDocumentosFirma` (`CODIGO='0003'`, `ACTIVO='SI'`). La web solo muestra el visor y dispara la descarga.
- ✅ `ReportesController`: `catalogo`, `firmas`, `{clave}/datos`, `{clave}/pdf`, `{clave}/csv`, más `impresion/orden/{numero}` y `impresion/requisa/{numero}` para las acciones de cada módulo. Los `RAISERROR` se reenvían sin traducir.
- ✅ `/reportes`: selección de reporte, parámetros según su tipo, vista previa acotada, descarga PDF/CSV y firma del documento 0003.
- ✅ Acciones de impresión en `/ordenes` (`OT.IMPRIMIR`) y `/requisas` (`REQUISAS.IMPRIMIR`), con tooltips que explican la validación del SP.
- ✅ Permisos sembrados: módulo `REPORTES` con `REPORTES.VER`, `.IMPRIMIR`, `.EXPORTAR`; Administrador y Supervisor los tres, Vendedor solo `VER`.
- ✅ 8 pruebas de `ServicioReportes` con doble de `ILectorSql`: catálogo, SQL parametrizado, conversiones, validación, límite del visor, firmas, CSV y propagación del mensaje legacy.

**Quirks heredados documentados (no se corrigieron en el SP):**
- `SINCO_CONSULTAR_RESUMENCOSTOS_SEMANAL` ignora `@Anyo`/`@Mes` y siempre usa `GETDATE()`; el visor lo advierte en la descripción del reporte.
- `SINCO_SP_CARTERA_ANTIGUEDAD` consulta `FUMIPLAGA.FMP`, así que requiere ese catálogo accesible desde `SINCOERPORKIN`.

**Dependencias:** Fases 4 y 5. **Riesgo:** medio — resuelto sin migrar `ReportViewer`: el contrato es el dataset del SP y el PDF se arma en la API, así que la plantilla visual se puede ajustar después sin tocar la firma del endpoint.

---

### Fase 8 — Notificaciones ✅ Completada (commit `68fa784`)
**Objetivo:** sustituir consola + Windows Service por procesos confiables.

**Entregables**
- ✅ `NotificacionBackgroundService` con intervalo, lote, reintentos con backoff acotado y auditoría en `int.JobEjecucion`. El job nace apagado (`Notificaciones__HabilitadoJob=false`).
- ✅ `IProveedorCorreo` → `ProveedorCorreoSmtp`: `SmtpClient` real configurado por `.env` (`Correo__*`), validado al arrancar. Nace apagado (`Correo__Habilitado=false`) para no enviar correo a destinatarios reales por accidente. La clave nunca se devuelve por API.
- ✅ Cola y bandeja sobre `dbo.NotificacionEnviada` extendida (sin tablas paralelas): `CuerpoHtml`, `ClaveNegocio` con deduplicación, `ProximoIntentoUtc` y `LeidoUtc`.
- ✅ SPs `SINCO_NOTIFICAR_*` leídos como fuentes parametrizadas y validadas: se comprueban `OBJECT_DEFINITION` (solo lectura) y `HAS_PERMS_BY_NAME(...,'EXECUTE')` antes de invocarlos. Se usan `CORREOS_ENVIADOS` y `REQUISAS_ERRADAS` (esta última solo con `Notificaciones__CorreoInterno` configurado); `CLIENTES` y `PROXIMOS_SERVICIOS` quedan como contrato validado por ser redundantes o exigir `@CodigoCliente`. No se invocó ningún SP que mutara datos.
- ✅ Campana en el sidebar con badge y sondeo cada minuto; página `/notificaciones` con bandeja, configuración del SMTP/job y acciones sensibles (`Recoger`, `Procesar cola`, `Reenviar`, `Marcar todas`) con tooltips y confirmaciones.
- ✅ Permisos sembrados: `NOTIFICACIONES.VER` (Administrador, Supervisor, Vendedor) y `NOTIFICACIONES.CONFIGURAR` (Administrador, Supervisor).

**Verificación:** build `0 Errores`; 168/168 pruebas — incluyen backoff, guardián de SPs, validación de opciones y servidor SMTP local en `127.0.0.1` con direcciones `ejemplo.local`; API y web levantadas en Development; `bandeja` y `configuracion` responden `200` sin exponer secretos; gating de campana y ruta comprobado; confirmación de `Procesar cola` cancelada sin ninguna petición de envío.

**Dependencias:** Fase 3. **Riesgo:** alto — resuelto sin migrar `MessageBox` ni credenciales incrustadas: el legacy nunca tuvo correo saliente y no había nada que reutilizar.

---

### Fase 9 — Pricing ✅ Cerrada por decisión (preparado, fuera del menú)
**Objetivo:** resolver la opción inerte del legacy sin construir un módulo sin regla de negocio.

**Decisión cerrada:** `PRICING` se **retira del menú por ahora**. No se elimina la estructura: el espacio en el roadmap y el contrato de abajo quedan listos para reactivar el módulo si el cliente lo pide.
> Aclaración de implementación: no se siembran hoy permisos `PRICING.*` sin un módulo que los use (D-21: un solo catálogo, una sola API). Los códigos y su destino quedan documentados; se crean en la siembra cuando el cliente defina la regla de negocio.

**Entregables**
- ✅ Opción `PRICING` ausente en el sidebar (comprobado en el menú de la web; el legacy no tiene handler).
- ✅ Contrato documentado; implementación diferida.

**Contrato preparado (no implementado)**

| Pieza | Compromiso |
|---|---|
| Datos | Sin tabla nueva. Precios y vigencias se leen del catálogo compartido existente: `dbo.PrecioProducto`, `dbo.PrecioActivo`, `dbo.TipoCambio` y `dbo.Producto.PrecioVentaActual` (D-20/D-21). El motor de cálculo sigue usando `CostoActual`; `PrecioVentaActual` es informativo. |
| API | Una sola API de catálogos; no se crea `/api/pricing`. Si se reactiva, se agrega un endpoint al controlador de catálogos con el mismo `Id` de producto. |
| Permisos prospectivos | `PRICING.VER`, `PRICING.EDITAR`, `PRICING.APROBAR`, con el separador de `SEG.PERMISOS_SEPARADOR`. No se asignan a roles hoy. |
| Regla de negocio | Bloqueante: el legacy no tiene handler y no hay regla de aprobación documentada. No se inventa flujo, estado ni tabla; se reactiva solo con una regla explícita del cliente. |
| UI | Ruta futura `/pricing` dentro de `Cotizaciones`, no un botón suelto. Hoy no existe. |

---

### Fase 10 — Corte, endurecimiento y puesta en producción
**Objetivo:** retirar el WinForms sin dejar huecos.

- Auditoría completa de operaciones sensibles (ya existe `aud`).
- Rotación de credenciales; eliminación de OAuth y passwords incrustados del legacy.
- Paralelo controlado: lectura en legacy, escritura en web → luego solo web.
- Capacitación y manual por módulo.
- Apagado de instaladores, consola y Windows Service.
- README y docs actualizados con el estado final.

---

## 5. Convenciones transversales (obligatorias en todas las fases)

1. **Permisos por módulo:** `{MODULO}.VER`, `.EDITAR`, `.APROBAR`, `.ANULAR`, `.IMPRIMIR`, `.CONFIGURAR`. Toda acción sensible lleva `[Authorize("...")]`; `401` redirige a login.
2. **Procesos protegidos:** cada cargador nuevo trae rama de simulación; un solo flujo activo por `(conexión, catálogo)`; alquiler con caducidad y fuente de verdad `TieneCorridaEnCurso`.
3. **Sin SQL concatenado.** Parámetros siempre. Sin credenciales en código ni en `appsettings` versionados.
4. **Catálogos del ERP:** homologación en tablas, visible en la grilla de mapeo del flujo ETL, sin JSON crudo como configuración visible.
5. **Baja lógica**, nunca borrado físico sin confirmación explícita. Todo cambio queda en `aud`.
6. **UI coherente:** sidebar de la plataforma con grupos estables (Cotizaciones, Catálogos, Operación, Administración), color corporativo `#E31837`, dashboard con cards KPI y tabla debajo, formularios unificados.
7. **Datos limpios:** solo lo que viene del ERP; sin registros de prueba; cada carga con `OrigenDatoId`, `ErpConexionId` y hash de contenido.

---

## 6. Decisiones cerradas

| # | Decisión | Resolución |
|---|---|---|
| 1 | `PRICING` inerte en el legacy | **Retirado del menú**; estructura y permisos listos para reactivarlo si se pide. |
| 2 | Google Calendar vs agenda propia | **Agenda propia con importador** y UI parecida a Google Calendar para reducir el impacto del cambio; migrar los eventos existentes a la agenda propia. |
| 3 | Exactus | **Envolver los SPs actuales** con parámetros y registrar resultado. |
| 4 | Reportes RDLC | **Reutilizar datasets/SPs y generar PDF desde la API**; la plantilla visual se migra después. |
| 5 | Estados `GE/AP/AN` | Catálogo configurable con esos mismos códigos para no romper reportes ni SPs. |
| 6 | Usuarios legacy `Seguridad_Usuarios` | Mapear a `seg.Usuario` e importar sin passwords en claro. |
| 7 | **D-20 — catálogos duplicados** | **No copiar a la plataforma catálogos que ya existen en SOFTLAND/FUMIPLAGA o en `SINCOERPORKIN.vv*`.** Solo modelar localmente lo que el cotizador realmente necesita y no tiene origen. Se cancela `AgregarCatalogosLegacy` y se retiran los 6 catálogos nuevos. |
| 8 | **D-21 — catálogo único y menú** | Una sola copia de cada catálogo maestro, una sola API por entidad y un solo grupo `Catálogos` en el menú, consumido por cotizador y por los módulos legacy. Una sola fuente (Softland / SINCOERPORKIN); no se crean catálogos por aplicación. |
| 9 | **D-22 — BD legacy existente** | Las capacidades del WinForms operan sobre **las tablas que ya existen** en `SINCOERPORKIN` (`Plantilla_Orden_Trabajo*`, `Plantilla_Calendario`, `TmpPlantillas`, etc.). Se migra la API y el frontend, **no el esquema**: no se crean tablas nuevas ni se importan datos a tablas paralelas. Se corrige la Fase 2, que había creado `dbo.Plantilla` y sus hijas; esas tablas se eliminaron con `PlantillasSoloLegacy`. |
| 10 | **Calendarios por técnico** | Cada técnico activo del catálogo único es un calendario con su color (D-21). El color se guarda en `dbo.Tecnico.ColorCalendario` y si falta se deriva de forma determinística; se permite activar/desactivar calendarios y ver una vista `Semana × técnico`. No se crea una tabla de calendarios ni se guarda el color en JSON. |
| 11 | **Excepción permitida en la Fase 3** | El importador de Google sí agrega tres tablas `int.*` de staging/auditoría/homologación; no son copias de catálogos ni de visitas, por lo que no violan D-20/D-22. |

---

## 7. Decisión pendiente de negocio (D-23) — Cotización → plantilla

> **Origen:** este puente **no existe en el legacy**. El cotizador es nuevo en este proyecto y la plantilla es un concepto operativo del WinForms. Por eso es una decisión de producto nueva, **no una migración**, y no se incluye en Fase 10.

**Validación del flujo:** no es correcto crear la plantilla automáticamente al aprobar. `APROBADA` significa **aprobación interna, todavía no enviada al cliente** (`dbo.EstadoCotizacionCatalogo.json:7`); la señal de “ya nació el servicio” es `CONVERTIDA_EN_SERVICIO` + `ConvertidaUtc` (`:12`, `CotizacionEntities.cs:171-172`). Tampoco hay hoy un flujo de aprobación ejecutable: están el esquema, los estados y los permisos, pero no hay endpoint/UI que escriba `AprobadaUtc`/`AprobadaPor`. La conversión debe ser una **acción explícita y asistida**, nunca un efecto oculto del cambio de estado.

**Evidencia de que no hay precedente:** búsqueda en `sinco_wos` de `cotiz|Cotiz|COTIZ` → 0 coincidencias; en `SINCOERPORKIN`, 0 SPs con `PLANTILLA` en el nombre o `COTIZ` en la definición. La plantilla siempre se armó a mano en `FrmCrudPlantillas`.

**Contrato propuesto (no implementado)**

| Pieza | Compromiso |
|---|---|
| Señal de entrada | `CONVERTIDA_EN_SERVICIO` con `ConvertidaUtc` (aceptación/orden de compra en ERP, D-20). No `APROBADA`. La aprobación interna queda en `APROBACIONES.DECIDIR` + `AprobadaUtc`/`AprobadaPor`; el envío al cliente en `COTIZACIONES.ENVIAR`. |
| Datos precargables | `Cliente` desde `dbo.Cliente` (resolviendo `Codigo` vs `CodigoExterno`); `DireccionServicio`; `Tecnico` desde `dbo.Tecnico`; `TipoServicio` → `CodigoServicio`; insumos → `CodigoArticulo`, `DescripArticulo`, `Cantidad`, `Umedida`, `CostoUnitario` y `CostoTotal`. `Contacto`/`Telefono` solo si se llegan a poblar hoy (el controlador actual no los llena). |
| Datos que el usuario debe completar | Locación (`vvLocacionesClientes`), frecuencia (`vvFrecuencia`), vehículo, plaga, lugar a tratar, dosificación, método, área, concentración, `MAXIMO_PERMITIDO`, `UTILIZADO` y los códigos ERP efectivos. No se inventan. |
| Equipos | **No mapear `DetalleAmortizable` a `Plantilla_Orden_Trabajo_Equipos`**: son conceptos distintos (activo amortizable vs catálogo `vvTipodeEquipos`). Los equipos se eligen del catálogo ERP. |
| Escritura | Solo al confirmar, vía `PlantillaService.CrearAsync` sobre `Plantilla_Orden_Trabajo*` (D-22). El prellenado no escribe. Sin columnas nuevas en tablas legacy y sin tablas paralelas. |
| Permisos | `COTIZACIONES.CONVERTIR` (ya sembrado, hoy solo Administrador) para la acción, más `PLANTILLAS.EDITAR` para escribir en plantillas. Separado de `APROBACIONES.DECIDIR` y `COTIZACIONES.ENVIAR`. |
| Idempotencia | Usar `CONVERTIDA_EN_SERVICIO` + `ConvertidaUtc` como guard; bloquear una segunda conversión. Si hace falta además el número de plantilla, se propone una columna nullable en `dbo.Cotizacion` (tabla de la plataforma, nunca en la tabla legacy). No escribir el número en `NOTAS` sin decisión de negocio. |
| Datos reales | Verificado el 2026-11-18: `dbo.Cotizacion` y `dbo.CotizacionAprobacion` están vacías. No hay evidencia para probar el mapeo. |

**Preguntas abiertas para el cliente**

1. ¿La plantilla es un **molde reutilizable** o la **ejecución única** del servicio cotizado? Si es ejecución única, el destino natural es una orden de trabajo, no una plantilla.
2. ¿La conversión ocurre al aprobar, al enviar o solo al aceptar/orden de compra? La recomendación es **solo al aceptar**.
3. ¿Cliente nuevo sin `ClienteId`/código ERP queda bloqueado hasta su alta? Recomendación: sí.
4. ¿Qué códigos se usan: `Codigo` o `CodigoExterno` para cliente, artículo, servicio y técnico?
5. ¿Las cantidades `decimal(18,4)` se redondean a `decimal(18,0)`, se bloquean o cambian de unidad? Hoy el servicio redondea y `COSTO_TOTAL` se calcula con la cantidad original, no con la redondeada.
6. ¿Se autoriza escribir trazabilidad en `NOTAS`? Recomendación: no.

Mientras no haya respuesta, queda documentado y **no se implementa**. No es Fase 10; se propone como trabajo nuevo posterior al corte (Fase 11 del README), después de cerrar la aprobación de cotizaciones.

---

## 8. Análisis D-24 — Catálogos: vistas `vv*` vs ETL

> **Estado:** 🟡 Análisis cerrado; decisión de implementación pendiente. No se elimina ni se reconfigura nada en Fase 10 sin instrucción explícita.

**Pregunta de origen:** si las vistas `vv*` ya entregan los datos del ERP, ¿para qué sirve el ETL y podríamos dejar todo como en el legacy a nivel de carga de catálogos?

### 8.1 Qué hay realmente

Verificado el 2026-11-18 con consultas de **solo lectura** a `SINCOERPORKIN`:

| Hallazgo | Dato real |
|---|---|
| Ubicación de las vistas | La API apunta a `10.40.50.1 / SINCOERPORKIN`, **la misma base** donde viven las `vv*`. No es un ERP remoto. |
| Flujos configurados | 2 conexiones, 8 flujos, 14 ejecuciones. **7 de 8 flujos** leen `dbo.vv*` y escriben tablas `dbo.*` de esa misma BD. |
| Único origen externo | `FUMIPLAGA.FMP.TIPO_CAMBIO_HIST` para tipos de cambio. |
| Disparo | Los 8 flujos son manuales (`TipoDisparador = 1`), con `UltimaEjecucionUtc` del 2026-11-17 y sin `ProximaEjecucionUtc`. No hay programación activa. |
| Versionado | No hay seeds de `int.ErpConexion` ni `int.ErpFlujo`. El GUID `80ae2da2-…` del catálogo de técnicos existe en la BD real, pero en el repositorio solo aparece en documentación. |
| Motor implementado | 4 tablas núcleo (`ErpConexion`, `ErpFlujo`, `SincronizacionEjecucion`, `ErpMapeoTipoCliente`), 17 endpoints en `/api/origenes`, 8 loaders `ICargadorCatalogo`, hosted service con reclamo atómico, simulación, pausa y bitácora. |
| Motor **documentado pero no implementado** | `ErpOrigen`, `ErpMapeoCampo`, `ErpValidacion`, `ErpRechazo`, `ErpDestino`, `ErpDependencia`, `ErpSimulacion`, `EventoRecibido`. No deben citarse como existentes. |
| Controles decorativos | `LoteRegistros`, `MaximoRegistrosPorCorrida`, `ModoExtraccion` y el disparador `EVENTO` se persisten pero no se aplican en `CorrerFlujoAsync`. |

### 8.2 Dos canales que hoy conviven

| Canal | Entidades | Fuente | Consumo |
|---|---|---|---|
| **Maestros materializados** | `Cliente`, `Producto`, `UnidadMedida`, `CategoriaProducto`, `TipoServicio`, `TipoCambio`, `Tecnico`, `ActivoAmortizable` | ETL/upsert + CRUD web + semilla | Cotizaciones por `Guid` con FK real; combos y nombres en plantillas, órdenes, visitas y requisas |
| **Códigos operativos en vivo** | Locación, bodega, frecuencia, plaga, ubicación, dosificación, método, equipo, vehículo | Solo vistas `vv*` | `/api/catalogos/opciones-erp`, validaciones y grillas; se guarda el **código ERP como texto**, sin tabla local |

El riesgo no es que existan dos canales, sino que los diálogos legacy **mezclen ambos**: guardan un código ERP string pero resuelven el nombre desde una tabla local. Si el espejo local se desactualiza, el código sigue siendo correcto y la grilla muestra un nombre viejo o vacío.

### 8.3 Los espejos locales no son copias exactas

| Entidad | Tabla local | Vista ERP | Diferencia |
|---|---:|---:|---|
| `Cliente` | 935 | 935 | Cuadra |
| `Producto` | 188 | 201 (`vvArticulos`) | Cuadra con la partición 188 productos + 13 amortizables |
| `Tecnico` | 14 | 14 | Cuadra |
| `UnidadMedida` | 19 | 7 | 12 filas adicionales de semilla `EXCEL`/`SISTEMA` |
| `CategoriaProducto` | 28 | 18 | 10 filas adicionales `MANUAL`/`EXCEL`/`SISTEMA` |
| `TipoServicio` | 54 | 43 | 11 filas adicionales `EXCEL` |

`OrigenDato` real: `1 = MANUAL`, `2 = EXCEL`, `3 = ERP`, `4 = SISTEMA`. Esas filas adicionales no son `PRUEBA/TEST/DEMO`, pero **tampoco vienen del ERP**: son complementos del cotizador. Borrar las tablas locales o el ETL sin migrarlas perdería ese dato.

### 8.4 Qué pasa si el ETL desaparece mañana

**Se rompe:** alta/cálculo de cotizaciones (FKs por `Guid`), combos de cliente/producto/técnico/tipo de servicio, nombres en grillas legacy y, sobre todo, la vigencia de `TipoCambio` (precios incorrectos en fechas futuras).

**Sigue funcionando:** opciones `vv*`, validación de plantillas e importador de Google, bodegas/locaciones en requisas, descripciones de inventario y firmas de reportes.

Conclusión: la degradación sería **parcial y silenciosa**, no un apagón visible. Eso es peor que un error explícito.

### 8.5 Riesgos de conservarlo tal cual

- `ErpFlujo.Origen` acepta **SQL arbitrario** y `LectorSqlServer` lo ejecuta; `EsSoloLectura = 1` solo protege nuestra tabla, no lo que el usuario de BD puede hacer en el ERP.
- La conexión usa `TrustServerCertificate = true` y no `ApplicationIntent=ReadOnly`.
- La UI de "Orígenes de datos" está expuesta a Administrador/Gestor aunque no haya flujos nuevos que configurar.
- La carga Excel comparte `ProcesoCargaService` con el ETL; eliminar el motor rompería `/api/carga/excel/PRODUCTOS`.

### 8.6 Decisión propuesta (supersedida por §8.7 y §8.8)

> Esta recomendación quedó superada: el usuario autorizó migrar el cotizador a códigos ERP y retirar los espejos. Se conserva como registro del análisis inicial.

**Conservar el núcleo y los 8 loaders; no eliminar tablas ni FKs de procedencia en Fase 10.** El ETL hoy no es una integración externa: es el mecanismo de refresco de los maestros con `Id` propio del cotizador y el mismo orquestador que usa la carga Excel. Sí sobra, en cambio, la narrativa de "motor ETL completo con mapeo declarativo"; las tablas de homologación y validación del diseño original no existen en código.

Trabajo recomendado, **fuera de Fase 10** salvo las comprobaciones documentales:

1. Declarar una sola fuente por entidad: tabla local para los 8 maestros; `vv*` solo para códigos sin tabla local.
2. Unificar los combos legacy para que dejen de mezclar ambos canales: si el campo guarda un `Guid`, usar la API local; si guarda un código ERP, usar `opciones-erp`.
3. Endurecer antes de producción: allowlist de vistas/tablas en lugar de SQL libre, `ApplicationIntent=ReadOnly` y usuario de BD con `SELECT` restringido.
4. Implementar o retirar de DTO/UI `LoteRegistros`, `MaximoRegistrosPorCorrida`, `ModoExtraccion` y el disparador `EVENTO`.
5. Solo si se decide ir a un modelo puramente `vv*`: primero verificar que ningún módulo depende de `ErpConexionId`/`UltimaSyncUtc`; después quitar FKs e índices de procedencia con migración explícita; por último retirar loaders y endpoints. **Nunca al revés.**

**Preguntas abiertas**

1. ¿Los maestros del cotizador son canónicos en `dbo`, o se migran las FKs a códigos ERP y se eliminan las tablas materializadas?
2. ¿Qué se hace con las filas `EXCEL`/`SISTEMA` que no existen en las vistas?
3. ¿Se autoriza el endurecimiento de SQL/TLS descrito en el punto 3?
4. ¿Se unifican los combos legacy en Fase 11?

Mientras no haya respuesta, queda documentado y **no se cambia código**. El menú ya se llama solo `Catálogos`; "Orígenes de datos" sigue siendo un enlace aparte con permiso `CATALOGOS.ORIGENES`.

### 8.7 Reevaluación — ¿Vale la pena migrar al formato legacy?

**Sí, y matiza la recomendación de §8.6:** no se trata de migrar el ETL al formato legacy, sino de **dejar de usarlo para catálogos y migrar el cotizador al formato legacy: códigos ERP + vistas `vv*`**. El ETL actual solo materializa en `dbo` lo que la vista ya entrega en la misma base; la duplicación es real para `Cliente`, `Producto`, `TipoServicio`, `Tecnico`, `UnidadMedida`, `CategoriaProducto` y `ActivoAmortizable`. No hay duplicación en los códigos operativos (frecuencia, locación, bodega, plaga, equipo, vehículo), que ya se leen en vivo.

**Por qué ahora es el momento barato:** verificado el 2026-11-18, `dbo.Cotizacion` y `dbo.CotizacionAprobacion` están vacías. No hay histórico que reconvertir de `Guid` a código ERP. Las tablas legacy ya guardan códigos y validan contra `vv*`, así que el destino no es un diseño nuevo: es el que ya usa el WinForms.

**Beneficio:** desaparecen 7 flujos espejo, 7 tablas duplicadas, los combos mixtos y el riesgo de nombres desactualizados. Queda una sola fuente por entidad y el cotizador deja de depender de un cargador para poder dar de alta una cotización.

**Costo real:** SQL Server no permite FK hacia una vista, así que se pierde la integridad referencial declarativa de `Cotizacion` y hay que validar en aplicación, igual que ya hace `PlantillaService` con `vv*`. Además hay que decidir el destino de las filas `EXCEL`/`SISTEMA` que no existen en el ERP y mantener aparte el único flujo con origen externo real: `TipoCambio` desde `FUMIPLAGA`.

**Recomendación revisada (autorizada por el usuario el 2026-11-18; ver §8.8):**

1. No crear más flujos espejo desde `vv*`; congelar los existentes.
2. Refactorizar `Cotizacion` para guardar códigos ERP y validarlos contra las vistas, sin columnas nuevas en tablas legacy.
3. Sustituir los combos locales de plantillas/órdenes/visitas/requisas por `opciones-erp` cuando el campo guarde código texto.
4. Retirar los loaders y tablas espejo, y dejar únicamente un lector estrecho para `TipoCambio` (o consulta directa parametrizada a `FUMIPLAGA`).
5. Ejecutarlo **antes del corte**, nunca después; no se inicia en Fase 10 sin instrucción explícita.

### 8.8 Decisiones del usuario sobre los objetos no-ERP (2026-11-18)

Principio rector confirmado: **el ERP es la fuente de verdad**. Todo vuelve a él —inventario, facturación y contabilidad—, así que el dato debe nacer bien ahí. No se homologa el desorden heredado del Excel.

| Grupo | Decisión | Consecuencia |
|---|---|---|
| **12 unidades locales** | El ERP prevalece. Aquí no se hace nada: se presentan solo las 7 unidades del ERP. Si falta una, se crea **en el ERP**, no en la plataforma. | Se descartan `ML`, `GR`, `LB`, `BLD`, `LT`, `GAL`, `LTS`, `M2`, `KM`, `HH`, `ROLLO`, `YARDA`. No se crean unidades locales ni se mapean alias. |
| **10 categorías locales** | Por ahora se mantiene `CLASIFICACION` tal cual la trae el ERP. Opcionalmente se agregará una vista nueva sobre `FUMIPLAGA.FMP.CLASIFICACION` para exponer `DESCRIPCION`, `AGRUPACION`, `USA_NUMEROS_SERIE`, `PLANTILLA_SERIE`, `APORTE_CODIGO`, `TIPO_CODIGO_BARRAS` y `UNIDAD_MEDIDA`. Después se ve si el cliente quiere cambiarla. | Se descartan las 10 categorías sembradas. El cotizador usa el código ERP (`01`–`99`) y su descripción. |
| **11 tipos de servicio locales** | Igual que las unidades: se presentan solo los `S000`–`S042` del ERP. Si el cliente quiere otros, se crean en el ERP o se decide después. | Se descartan `SAN`, `GEN`, `TER`, `CUC`, `MOS`, `ROD`, `GAR`, `MUR`, `ZOM`, `HOR`, `FOS`. No se mapean a códigos ERP "parecidos". |

Constatación previa: los 33 códigos tienen **0 usos** en productos, activos, márgenes, plantillas, órdenes, notificaciones, visitas, cotizaciones y detalles; `Cotizacion`, `CotizacionAprobacion`, `DetalleInsumo`, `DetalleAmortizable`, `TipoServicioMargen`, `PrecioProducto` y `PrecioActivo` están vacías. No hay datos que reconvertir.

### 8.9 Plan de implementación del refactor (antes del corte, fuera de Fase 10)

1. **Contrato de catálogos ERP.** Centralizar la validación contra `vv*` en un servicio único, reutilizando el patrón de `PlantillaService`. Exponer por `opciones-erp`: unidades desde `vvArticulos.UNIDAD_VENTA`, clasificaciones desde `vvArticulos.CLASIFICACION`, tipos de servicio desde `vvTipoServicios`, clientes, técnicos y códigos operativos. Si se agrega la vista sobre `FUMIPLAGA.FMP.CLASIFICACION`, mapearla como `DbSet` con `ExcludeFromMigrations()`, igual que las demás `vv*`.
2. **`Cotizacion` con códigos ERP.** Sustituir los `Guid` por códigos texto: `ClienteCodigo`, `TipoServicioCodigo`, `TecnicoCodigo`, `ProductoCodigo`, `UnidadMedidaCodigo`, `CategoriaCodigo` y `ActivoCodigo`. Quitar las FKs a tablas locales de `CotizadorDbContext` y validar en aplicación (SQL Server no permite FK hacia una vista). `TipoCambio` se resuelve por fecha/fuente, no por `Guid` local.
3. **Combos de la UI.** Cotizador y diálogos legacy pasan a `opciones-erp` cuando el campo guarde código; dejan de resolver nombres desde `dbo.Cliente`/`dbo.Tecnico`/`dbo.Producto`. Los campos que guarden `Guid` legítimo (parámetros, estados, permisos) se quedan en tablas locales.
4. **Retiro de espejos y ETL.** Quitar los 7 loaders espejo, los 17 endpoints de `/api/origenes`, el enlace "Orígenes de datos" y el permiso `CATALOGOS.ORIGENES`. Después de que ningún módulo lea las tablas, eliminar FKs de procedencia, índices y tablas con migración explícita, en ese orden. Queda únicamente un lector estrecho y parametrizado para `TipoCambio` desde `FUMIPLAGA`.
5. **Semillas.** Retirar los 33 códigos de `scripts/seed/dbo.UnidadMedida.json`, `dbo.CategoriaProducto.json` y `dbo.TipoServicio.json`, y actualizar sus comentarios para que el ERP figure como única fuente.
6. **Verificación.** Build de la solución, suite con el runner permitido y consultas de solo lectura para confirmar que no queden referencias a los códigos retirados. No se aplican migraciones a la BD real sin instrucción explícita.

---

## 9. Próximo paso concreto

Con D-20, D-21, D-22 y D-24 documentadas (D-24 ya decidida, con refactor pendiente) y las fases 0–9 completadas, el siguiente trabajo es la **Fase 10 — Corte, endurecimiento y puesta en producción**. No se inicia sin instrucción explícita: incluye rotación de credenciales, paralelo controlado (lectura en legacy, escritura en web) y apagado de instaladores, consola y Windows Service. El correo y el job de notificaciones siguen apagados por configuración hasta que se decida habilitarlos con credenciales reales. D-23 queda fuera de Fase 10 y solo se retoma con respuesta del cliente. D-24 quedó decidida y su refactor (§8.9) debe ejecutarse **antes** del corte, no dentro de Fase 10.

Para la Fase 10 quedan como comprobaciones locales, sin tocar producción: revisar la cobertura de `aud` en operaciones sensibles, confirmar que no queden passwords incrustados en código versionado, verificar que los flujos ETL no ejecuten SQL libre con credenciales privilegiadas (D-24) y actualizar README/docs con el estado final.

Principios que se mantienen para las fases restantes:

1. Mantener el principio D-22: operar sobre las tablas legacy existentes, sin tablas paralelas.
2. Reutilizar los SPs originales: `dbo.SINCO_GENERA_DOCINV_EXACTUS` y los de traslado/reportes, con parámetros y registro de resultado, sin reimplementarlos.
3. Reutilizar el catálogo único (técnicos, clientes, artículos, bodegas) y la API única; no duplicar catálogos por módulo (D-21).
4. Mantener el sidebar con `Catálogos` y agregar páginas a `Operación` solo cuando existan; nunca duplicar un catálogo entre grupos.

---

## Anexo A — Agenda de visitas tipo Google Calendar

La fuente de verdad es nuestra agenda, pero la interacción resulta familiar a quien hoy usa Google Calendar:

- ✅ **Vista mensual por defecto** con navegación rápida de mes (`‹`, `›`, `Hoy`) y encabezado en español.
- ✅ **Panel "Calendarios" a la izquierda**: un técnico activo = un calendario con su color, activable/desactivable. El conteo `N de M visibles` y el icono `Seleccionar todos` permiten enfocar la agenda.
- ✅ **Vista "Semana × técnico"**: fila por técnico y columna por día; una visita con varios técnicos aparece en cada fila, con el color de ese calendario. Es el plus de organización pedido por el usuario.
- ✅ **Cuadrícula de días** con chips coloreados por técnico (icono de evento o de orden generada), hora y título. Clic en un chip abre el panel lateral de detalle.
- ✅ **Panel lateral de detalle**: visita, cliente, locación/servicio, fecha/hora, vehículo/técnicos, plantilla/orden, nota y chips de técnicos asignados; acciones Editar, Generar OT y Reversar según permisos.
- ✅ **Filtros** por texto, cliente, plantilla y rango, más el filtro por calendarios activos (también aplicado a la pestaña `Lista`).
- ✅ **Importador** con vista previa: lista los eventos `Orkin -` de Google, muestra cuáles se pudieron homologar y cuáles se rechazaron (cliente/locación/servicio/vehículo/técnico) y permite corregir el mapeo antes de insertarlos en la agenda propia.
- ✅ **Trazabilidad:** cada visita importada conserva el `IdCalendario` del evento; el lote, los eventos y los rechazos se guardan en `int.ImportacionGoogleLote`, `int.ImportacionGoogleEvento` e `int.ErpMapeoEventoGoogle`.
- 🔶 **Diferido:** arrastrar para mover/redimensionar eventos y la vista día. No estaban en la primera entrega; la edición de fecha/hora se hace en el diálogo de visita.

> Objetivo de UX: que el usuario reconozca la pantalla y solo note mejoras (calendarios por técnico con color, integración con el resto del sistema y sin depender de OAuth para trabajar).
