# ORKIN — Roadmap de migración del legacy `sinco_wos`

> **Estado:** 🟢 Fases 0–3 completadas (sin push) · **Fecha:** 2026-11-17
> **Origen:** `D:\repos\sinco-orkin-lagacy\sinco_wos`
> **Destino:** plataforma web .NET 10 / Blazor WebAssembly / MudBlazor / EF Core 10, con `SINCOERPORKIN` como BD principal.
>
> **Principio rector:** no se migra código, se migran **capacidades**. Se conservan las reglas de negocio válidas y se corrigen los defectos del legacy (SQL concatenado, credenciales incrustadas, estados ambiguos, servicios incompletos). El cotizador es el **módulo 1**; este roadmap cubre el resto de las opciones que hoy solo existen en el WinForms.
>
> **Regla de oro de catálogos (D-20):** **no se copian a la plataforma catálogos que ya existen en SOFTLAND/FUMIPLAGA o en las vistas `vv*` de `SINCOERPORKIN`.** Solo se modelan localmente los datos que el cotizador realmente necesita y que no tienen origen en esos sistemas. Si el dato ya está en el ERP, se consume desde ahí (vista o conexión configurada), se homologa en tablas y se muestra en la grilla del flujo ETL; no se crea una tabla paralela "por si acaso".
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
| 2 | Menú principal | `Frmprincipal` | `MainLayout.razor` (sidebar MudBlazor por permisos) | 🔶 | 0/10 |
| 3 | Catálogo de artículos | `FrmPlantillaCatalogo` | `/catalogos/productos` + ETL desde `vvArticulos` | ✅ | 0/1 |
| 4 | Catálogo de dosificación | `FrmPlantillaCatalogo` | `vvDosificacion` se consulta desde `api/catalogos/opciones-erp`; **no se copia** y el editor muestra descripción + código | ✅ | 2 |
| 5 | Catálogo de frecuencia | `FrmPlantillaCatalogo` | `vvFrecuencia` se consulta desde `api/catalogos/opciones-erp`; **no se copia** | ✅ | 2 |
| 6 | Catálogo de técnicos | `FrmPlantillaCatalogo` | `/catalogos/tecnicos` + ETL `vvTecnicos` — sí se conserva: lo usa el cotizador | ✅ | 1 |
| 7 | Catálogo de servicios | `FrmPlantillaCatalogo` | `/catalogos/tipos-servicio` | ✅ | 0 |
| 8 | Catálogo de vehículos | `FrmPlantillaCatalogo` | `vvVehiculos` se consulta desde `api/catalogos/opciones-erp`; **no se copia** | ✅ | 2 |
| 9 | Catálogo de usuarios | `FrmPlantillaCatalogo` | `/administracion/usuarios` (seg.Usuario) | 🔶 | 1/9 |
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
| 25 | Documentos de inventario + detalle | `FrmDocumentoInventario` | `/inventario/documentos` | 🆕 | 6 |
| 26 | Generar / trasladar documento a Exactus | SPs `SINCO_GENERA_DOCINV_EXACTUS`, `SINCO_TRASLADAR_DOCINV_EXACTUS` | Servicio `IExactusService` envolviendo los SPs con parámetros | 🆕 decisión cerrada | 6 |
| 27 | Imprimir orden de trabajo | `FrmReportViewer` + `OrdenesDeTrabajo.rdlc` | `/ordenes/{id}/imprimir` — PDF generado en la API desde los SPs | 🆕 decisión cerrada | 7 |
| 28 | Reportes de requisas y costos | SPs `SINCO_IMPRIMIR_REQUISAS*`, `SINCO_CONSULTAR_*` | `/reportes` — PDF desde API + datasets existentes | 🆕 decisión cerrada | 7 |
| 29 | Notificaciones de visitas y correos | Consola + Windows Service + SPs `SINCO_NOTIFICAR_*` | `IHostedService` + proveedor de correo | 🆕 | 8 |
| 30 | Integración Google Calendar | `GoogleCalendar.cs` | **Agenda propia con calendarios por técnico y su color + importador** de eventos existentes | ✅ decisión cerrada | 3 |
| 31 | `PRICING` | Sin handler | **Retirado del menú**; estructura preparada para reactivarlo | ✅ decisión cerrada | 9 |
| 32 | `REPORTES` (botón del menú) | Sin handler | Se activa con la Fase 7 | 🆕 | 7 |
| 33 | `CAMBIAR CONTRASEÑA` (label) | Sin handler | Ya existe en `/cambiar-contrasena` | ✅ | 0 |
| 34 | Campana de notificaciones | Decorativa | implementar en Fase 8 | 🆕 | 8 |

---

## 3. Estrategia de datos

### 3.1 Tablas del legacy (26)

| Grupo | Tablas legacy | Acción |
|---|---|---|
| Ya cubiertas por el cotizador | `ParametrosGenerales`, `Seguridad_*` (parcial) | Mapear a `dbo.ParametroGeneral`, `seg.Usuario`, `seg.Rol`, `seg.Permiso` |
| Catálogos a crear/confirmar | Maestros `U_*` vía vistas `vv*` | ETL con homologación + simulación (patrón yaImplementado) |
| Núcleo operativo legacy (D-22) | `Plantilla_Orden_Trabajo`, `_Detalle`, `_Servicio`, `_Equipos` | **Tablas existentes, mapeadas tal cual** con `ExcludeFromMigrations()`. Clave `PLANTILLA` (`bigint`, sin identity), hijos con `int`, `ACTIVO = SI/NO`, consecutivos `MAX + 1`. No se crean tablas ni se importan datos. |
| Núcleo operativo nuevo | `Orden_Trabajo*`, `Requisa*`, `ProgramacionVisita*`, `Documento_Inv*` | Modelo EF Core propio sobre las estructuras del ERP, con `Codigo`/`Numero` consecutivo y auditoría |
| Técnicas/puente | `NotificarProgramacionVisitaTemporal`, `NotificacionesEnviadas`, `logs` | Sustituir por `int.JobEjecucion`, `aud.Notificacion`, `aud.Bitacora` |
| Núcleo operativo legacy (D-22) | `Plantilla_Calendario` | **Tabla existente**, mapeada tal cual: la agenda propia la usa para alta, edición y reversa. No se crean tablas de visitas paralelas. |

### 3.2 Stored procedures (29)

No se migran "como están". Se clasifican y envuelven:

| Categoría | SPs | Tratamiento |
|---|---|---|
| Generación de documentos | `SINCO_GENERA_ORDENE_DE_TRABAJO`, `SINCO_GENERA_ORDENES_DE_TRABAJO`, `SINCO_GENERA_REQUISA`, `SINCO_GENERA_DOCINV_EXACTUS`, `SINCO_TRASLADAR_DOCINV_EXACTUS` | Fases 4–6: envolver en servicios con parámetros; Fase 10: trasladar regla a C# y dejar SP de respaldo |
| Anulación / renumeración | `SINCO_ANULAR_ORDENES_DE_TRABAJO`, `SINCO_ELIMINAR_NUMERO_DE_ORDEN`, `SINCO_CAMBIAR_NUMERO_DE_ORDEN`, `SINCO_ELIMINAR_REQUISA` | Convertir a servicios con transacción y auditoría (nunca borrado físico sin confirmación) |
| Reportes | `SINCO_IMPRIMIR_ORDENE_DE_TRABAJO(N)`, `SINCO_IMPRIMIR_REQUISAS*`, `SINCO_IMPRIMIR_ORDENES_DE_TRABAJO` | Fase 7: el servicio de reportes los invoca y genera PDF |
| Notificaciones | `SINCO_NOTIFICAR_CLIENTES`, `SINCO_NOTIFICAR_PROXIMOS_SERVICIOS`, `SINCO_NOTIFICAR_CORREOS_ENVIADOS`, `SINCO_NOTIFICAR_REQUISAS_ERRADAS`, `SINCO_MOVER_NOTIFICACION_DESERVICIOS` | Fase 8: sustituir por `BackgroundService` + correo real con bitácora |
| Consultas de costos/cartera | `SINCO_CONSULTAR_*`, `SINCO_SP_CARTERA_ANTIGUEDAD` | Fase 7/9: proyectar como endpoints de reporte |
| Utilitarios | `SINCO_LIMPIAR_TABLAS_DE_TRABAJO`, `SINCO_ACTUALIZAR_DATOSLOCACIONES` | No migrar; sustituir por mantenimiento web |

> **Regla de oro:** ningún SP se invoca con SQL concatenado. Todos pasan por `SqlParameter` o por `FromSqlInterpolated`, con auditoría de quién lo ejecutó.

### 3.3 Vistas `vv*`

Las 16 vistas siguen siendo la fuente de los catálogos ETL (`vvArticulos`, `vvClientes`, `vvTecnicos`, `vvFrecuencia`, etc.). Se respeta el patrón actual: **homologación en tabla, mapeo visible en la grilla del flujo, simulación antes de ejecutar, una corrida activa por (conexión, catálogo)**.

### 3.4 Catálogo único compartido (D-20 / D-21)

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
- **Ejemplo resuelto:** frecuencias, locaciones, métodos de aplicación, tipos de plaga, tipos de ubicación y vehículos se retiraron del código porque ya están en el ERP. Cuando las órdenes de trabajo o las visitas los necesiten, leerán el mismo catálogo compartido, sin tablas nuevas.

### 3.5 Menú unificado

El sidebar es de la **plataforma**, no del cotizador:

| Grupo | Contenido | Estado |
|---|---|---|
| `Dashboard` | KPIs de toda la operación | ✅ |
| `Cotizaciones` | Módulo 1 | ✅ |
| `Catálogos compartidos` | Clientes, productos, unidades, categorías, tipos de servicio, tipos de cambio, técnicos y orígenes ETL | ✅ |
| `Operación` | Plantillas, visitas, órdenes, requisas, inventario, reportes | 🔶 Plantillas ✅ (Fase 2); resto 🆕 fases 3–7; solo se agregan links cuando la página exista |
| `Administración` | Usuarios, roles, parámetros, bitácora, cambiar contraseña | ✅ |

`PRICING` no aparece. `REPORTES` aparece dentro de `Operación` cuando la Fase 7 esté lista, no como botón suelto. Ningún grupo muestra dos veces el mismo catálogo.

---

## 4. Fases de migración

### Fase 0 — Fundación (en curso)
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

**Dependencias:** Fase 1 (catálogos compartidos). **Riesgo:** medio — resuelto respetando las claves compuestas y los códigos ERP del legacy, sin migración de datos ni reconciliación por `Id` artificial.

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

### Fase 4 — Órdenes de trabajo
**Objetivo:** reemplazar `FrmCrudOrdenes` y `FrmPlantillas` en modo `OrdenTrabajo`.

**Entregables**
- Listado con filtros por fecha/estado y acciones Nueva, Editar, Anular, Imprimir.
- Formulario con pestañas Productos, Servicios, Equipos, Técnicos y cabecera completa.
- Estados `GE` (generada), `AP` (aplicada), `AN` (anulada) como catálogo configurable.
- Generación de requisa desde la OT.
- Imprimir → Fase 7.
- Permisos: `OT.VER`, `.EDITAR`, `.ANULAR`, `.APROBAR`, `.IMPRIMIR`.

**Dependencias:** Fases 2 y 3. **Riesgo:** alto — es el corazón operativo.

---

### Fase 5 — Requisas
**Objetivo:** reemplazar `FrmCrudRequisas` y `FormDetalleRequisa`.

**Entregables**
- Listado en modo `RequisaGenerada` (Nueva, Editar, Anular, Imprimir).
- Cabecera: número de requisa, número de orden, cliente, locación, bodega, fechas, estado e instrucciones.
- Líneas con cantidad entregada / utilizada / devuelta y cálculo de SobFal.
- Asignación de técnicos y generación desde orden de trabajo.
- Permisos: `REQUISAS.VER`, `.EDITAR`, `.ANULAR`, `.IMPRIMIR`.

**Dependencias:** Fase 4. **Riesgo:** medio.

---

### Fase 6 — Documentos de inventario y Exactus
**Objetivo:** reemplazar `FrmDocumentoInventario` y el flujo de exportación.

**Entregables**
- Listado de documentos con filtro transferido `SI/NO`.
- Detalle por artículo y mensajes del proceso.
- Servicio `IExactusService` que envuelve los SPs de generación y traslado con parámetros y registro de resultado.
- Permisos: `INVENTARIO.VER`, `.EDITAR`, `.TRANSFERIR`.

**Dependencias:** Fase 5. **Riesgo:** alto — dependencia externa Exactus, pero con la interfaz ya definida: se envuelven los SPs actuales con parámetros y se registra el resultado de cada llamada.

---

### Fase 7 — Reportes e impresión
**Objetivo:** activar el botón `REPORTES` hoy inerte y dar salida PDF a OT y requisas.

**Decisión cerrada:** se reutilizan los datasets y los SPs `SINCO_IMPRIMIR_*` actuales, y el **PDF se genera desde la API**; la web solo muestra el visor y ofrece la descarga. No se migra `ReportViewer` al cliente.

**Entregables**
- Servicio de reportes en la API: invoca los SPs `SINCO_IMPRIMIR_*` con parámetros, arma el dataset y genera el PDF.
- Visor en la web y descarga; firma digital desde `vvTipodeDocumentosFirma` (`CODIGO='0003'`).
- Reportes de costos/transacciones/cartera como endpoints con filtros y salida PDF/Excel.
- Permisos: `REPORTES.VER`, `.IMPRIMIR`, `.EXPORTAR`.

**Dependencias:** Fases 4 y 5. **Riesgo:** medio — la plantilla visual del `.rdlc` se puede migrar después sin cambiar el contrato del endpoint.

---

### Fase 8 — Notificaciones
**Objetivo:** sustituir consola + Windows Service por procesos confiables.

**Entregables**
- `IHostedService` con programación, reintentos y auditoría (`int.JobEjecucion`, `aud.Notificacion`).
- Envío real de correo (proveedor configurable), reemplazando la lógica oculta en SPs.
- Campana de notificaciones del sidebar con estado de envíos.
- Permisos: `NOTIFICACIONES.VER`, `.CONFIGURAR`.

**Dependencias:** Fase 3. **Riesgo:** alto — no migrar `MessageBox` ni credenciales incrustadas.

---

### Fase 9 — Pricing (preparado, fuera del menú)
**Objetivo:** resolver la opción inerte del legacy sin construir un módulo sin regla de negocio.

**Decisión cerrada:** `PRICING` se **retira del menú por ahora**. No se elimina la estructura: los permisos `PRICING.VER`, `.EDITAR`, `.APROBAR` y el espacio en el roadmap quedan listos para reactivar el módulo si el cliente lo pide.

**Entregables**
- Opción `PRICING` ausente en el sidebar.
- Contrato de datos y permisos documentados; implementación diferida.

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
6. **UI coherente:** sidebar de la plataforma con grupos estables (Cotizaciones, Catálogos compartidos, Operación, Administración), color corporativo `#E31837`, dashboard con cards KPI y tabla debajo, formularios unificados.
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
| 8 | **D-21 — catálogo compartido y menú** | Una sola copia de cada catálogo, una sola API y un solo grupo `Catálogos compartidos` en el menú, consumido por cotizador y por los módulos legacy. Todo termina viniendo de Softland; no se crean catálogos por aplicación. |
| 9 | **D-22 — BD legacy existente** | Las capacidades del WinForms operan sobre **las tablas que ya existen** en `SINCOERPORKIN` (`Plantilla_Orden_Trabajo*`, `Plantilla_Calendario`, `TmpPlantillas`, etc.). Se migra la API y el frontend, **no el esquema**: no se crean tablas nuevas ni se importan datos a tablas paralelas. Se corrige la Fase 2, que había creado `dbo.Plantilla` y sus hijas; esas tablas se eliminaron con `PlantillasSoloLegacy`. |
| 10 | **Calendarios por técnico** | Cada técnico activo del catálogo compartido es un calendario con su color (D-21). El color se guarda en `dbo.Tecnico.ColorCalendario` y si falta se deriva de forma determinística; se permite activar/desactivar calendarios y ver una vista `Semana × técnico`. No se crea una tabla de calendarios ni se guarda el color en JSON. |
| 11 | **Excepción permitida en la Fase 3** | El importador de Google sí agrega tres tablas `int.*` de staging/auditoría/homologación; no son copias de catálogos ni de visitas, por lo que no violan D-20/D-22. |

---

## 7. Próximo paso concreto

Con D-20, D-21 y D-22 cerrados y las fases 0–3 completadas, el siguiente trabajo es la **Fase 4 — Órdenes de trabajo**:

1. Mantener el principio D-22: operar sobre `Orden_Trabajo*` y `Plantilla_Orden_Trabajo*`, sin tablas paralelas.
2. Reutilizar el catálogo único compartido (técnicos, clientes, plantillas) y la API única; no duplicar catálogos por módulo (D-21).
3. Respetar la regla legacy: una plantilla no se elimina físicamente mientras tenga filas en `Plantilla_Calendario`.
4. Aprovechar el puente ya construido: `IGeneradorOrdenTrabajo` y la reversa de visitas de la Fase 3.
5. Mantener el sidebar con `Catálogos compartidos` y agregar páginas a `Operación` solo cuando existan; nunca duplicar un catálogo entre grupos.

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
