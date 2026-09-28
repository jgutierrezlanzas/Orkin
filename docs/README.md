# ORKIN · Cotizador Web — Documentación

> Índice de la documentación técnica del proyecto. El **README de trabajo** (fases, tareas, estados,
> decisiones) vive en la raíz del repositorio: [`../README.md`](../README.md).

## Documentos

| # | Documento | Qué contiene | Estado |
|---|---|---|---|
| 00 | [`../README.md`](../README.md) | Análisis del Excel, reglas de negocio, hallazgos, decisiones, arquitectura, estructura de solución, **plan de fases con estados** | 🟡 En curso |
| 01 | [`BASE-DE-DATOS.md`](BASE-DE-DATOS.md) | **Estructura completa de la base de datos** en SQL Server: esquemas, tablas, columnas, tipos, índices, `CHECK`, vistas, scripts, y el **motor ETL de integración con ERP** (§7) | 🔵 En revisión |
| 02 | [`INTEGRACION-ERP.md`](INTEGRACION-ERP.md) | Diseño conceptual del ETL con ERP: **alcance de una sola dirección (D-19)**, flujo, origen, transformación, validación, destino, **alta y baja de productos**, **frecuencia recomendada**, conflictos, riesgos y plan de la Fase 12 | 🟡 Diseño cerrado, no implementado |
| 03 | `MIGRACION-EXCEL.md` | Estrategia, mapeo y script de importación del `.xlsm` a las tablas del catálogo | ⬜ Se escribe en Fase 2 |
| 04 | `MOTOR-CALCULO.md` | Contrato del motor de cálculo, casos golden master contra el Excel y reglas corregidas | ⬜ Se escribe en Fase 3 |
| 05 | `SEGURIDAD.md` | Matriz de roles y permisos, emisión/validación de JWT, flujo de login y del enlace público de aprobación | ⬜ Se escribe en Fase 1 |
| 06 | `DESPLIEGUE.md` | Entornos, cadenas de conexión, migraciones por pipeline, IIs/Docker, respaldos, y manual de operación de integraciones | ⬜ Se escribe en Fase 9 |

## Leyenda de estados

| Estado | Significado |
|---|---|
| ⬜ **Pendiente** | No iniciado |
| 🟡 **En progreso** | En ejecución |
| 🔵 **En revisión** | Hecho, esperando validación del cliente |
| ✅ **Aprobado** | Validado por el cliente y en ejecución |
| ⏸️ **Postergado** | Fuera del alcance actual |
| 🔴 **Bloqueado** | Depende de una decisión o insumo externo |

## Regla de oro de esta documentación

> El Excel es la fuente de los **datos** (catálogos, precios, tipos de servicio, histórico de tipo de
> cambio), pero **no** de su lógica cuando esta es incorrecta.
>
> Todo hallazgo de §2.4 del README principal se corrige en el sistema, y **cada corrección requiere
> validación del cliente antes de implementarse** (decisión D-14 / ADR-012).
>
> Cuando se corrija una regla, este paquete de documentos debe quedar mostrando **las dos cosas**:
> lo que hace el Excel hoy y lo que hace el sistema. Sin ese contraste, nadie puede verificar nada.

## Documentos de origen (no son nuestros)

| Archivo | Naturaleza |
|---|---|
| `Cotizador Original - Agosto 31 2026.xlsm` | Herramienta actual de ORKIN. **Fuente de verdad de los datos.** No se modifica. |
| `Cotizador_Web_Documento_de_Arranque.docx` | Project Charter + SRS (v1.0) del cliente. Base del alcance. |
| `Cotizador Web — Prototipo D (sidebar + Cotizaciones).html` | **Diseño de UI aprobado.** Referencia visual. |
| `D:\repos\MerakodeSoft\Mikaela.Prestamos` | Proyecto de **referencia** de estructura de solución. No se replica código. |
