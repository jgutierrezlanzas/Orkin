# ORKIN — Integración con ERP

> **Estado:** 🟡 Diseño cerrado, **primera versión funcionando** (solo lectura, productos).
> **Alcance:** **una sola dirección** — el ERP alimenta los catálogos del cotizador. La web **nunca**
> escribe en el ERP del cliente (**D-19**).
> **Estructura de datos:** [`BASE-DE-DATOS.md`](BASE-DE-DATOS.md) §7.
> **Fecha:** 2026-09-27 · **Actualizado:** 2026-09-29 — ver §9, "Lo que ya está construido".
>
> La §9 describe lo que existe en el código hoy, que es menos que el diseño de las §1–8. Las
> secciones de diseño se conservan completas porque siguen siendo la referencia de lo que falta,
> no porque todo esté pendiente de hacer.

---

## 1. Qué se pidió y qué se propone

> "Que si un cliente tiene su ERP, sea cual sea, podamos vincularlo para que nuestras tablas se
> llenen con la data real del ERP del cliente, en vivo y en directo."

### 1.0 Alcance: una sola dirección (D-19)

> **"Lo que se cargará del ERP automáticamente son catálogos para que funcione el cotizador con
> datos reales: cambios de precios o costos, alta y baja de productos, etc. Actualmente no se está
> pensando que la web actualice el ERP del cliente. Quizás a futuro generar alguna orden de
> compra, pero ahorita eso está fuera del alcance."**

```
   ERP  ──────▶  Cotizador         ← LO ÚNICO QUE SE CONSTRUYE
   ✗            Cotizador ──────▶  ERP
```

| | |
|---|---|
| ✅ **Entra desde el ERP** | Productos (alta y baja), precios, costos, activos amortizables, tipo de cambio, unidades de medida, categorías, tipos de servicio |
| ⛔ **Nunca sale hacia el ERP** | Nada. Ni maestros, ni cotizaciones, ni órdenes |
| 🔮 **Futuro, no comprometido** | Orden de compra u orden de venta (D-19, §7.16 de `BASE-DE-DATOS.md`) |

**Tres consecuencias prácticas de que sea de una sola dirección:**

| Consecuencia | Detalle |
|---|---|
| El usuario SQL que pedimos al DBA del cliente es `db_datareader`, y nada más | No hay que pedir permiso de escritura. Se aprueba en una línea y sin burocracia |
| `int.ErpConexion.EsSoloLectura` tiene `CHECK (EsSoloLectura = 1)` | La base de datos **impide** que alguien marque una conexión como escribible. No es una convención: es una restricción |
| `IErpConector` solo expone `Leer` | La interfaz no tiene `Escribir`, `Actualizar` ni `Eliminar`. No es que "no se llame": es que **no existe** |

> **Por qué esto es una ventaja y no una carencia.** Una integración de solo lectura es más fácil de
> conseguir, más segura de operar y no genera un solo incidente de datos en la base del cliente. La
> conversación con su DBA, cuando llegue, va a ser: *"¿autorizan que escribamos?"*. Si dicen que
> sí, se implementa la orden de compra en una fase aparte. Si dicen que no —y es la respuesta más
> probable—, se resuelve con una exportación de archivo y **el cotizador no se ve afectado**.

### 1.1 Las promesas del cliente, una por una

| Promesa | Cómo se cumple |
|---|---|
| "sea cual sea el ERP" | 10 conectores previstos + uno genérico HTTP+JSON. El **mapeo es declarativo (grilla)**, no código: un ERP con columnas raras se integra configurando, no programando. |
| "nuestras tablas se llenen" | `dbo.Producto`, `ActivoAmortizable`, `TipoServicio`, `UnidadMedida`, `CategoriaProducto`, `TipoCambio` ya tienen `OrigenDato`, `ErpConexionId`, `CodigoExterno` y `HashContenido`. |
| "datos reales" | El catálogo que usa el motor de cálculo es el del ERP, no una copia del Excel |
| "cambios de precios o costos" | `dbo.PrecioProducto` es **historial**: cada cambio de costo o de precio inserta una fila con su fecha. Se puede ver cuándo y por qué cambió cada precio |
| "alta y baja de productos" | Alta = `INSERT` con `OrigenDato = 2`. Baja = `EsActivo = 0`, **nunca `DELETE`** (§5) |
| "en vivo y en directo" | Tres modos de disparo: **evento** (webhook, 2–5 s), **programado** (30 s a 15 min) y **híbrido**. |

### 1.2 Qué se integra primero

| Prioridad | Entidad | Por qué en ese orden |
|---|---|---|
| 1 | **Unidad de medida** | Maestro. Sin `LT` / `ML` / `UND` no se puede guardar un producto |
| 2 | **Categoría de producto** | Maestro. Corre antes que productos para que el enriquecimiento no cree duplicados |
| 3 | **Producto** (alta y baja) | Sin productos no hay cotización. Es el bloqueante |
| 4 | **Precio de producto** (precio de venta y costo) | Es el valor de la integración: lo que cambia seguido |
| 5 | **Tipo de cambio** | Sin TC no hay conversión a US$. Debe estar listo antes de las 8 a. m. |
| 6 | **Activo amortizable** | Necesario para la depreciación mensual (Fase 3) |
| 7 | **Tipo de servicio** | Cambia muy poco. Puede seguir editándose a mano |
| 8 | **Cliente** | Fuera del cotizador; relevante en el módulo de clientes (Fase 11) |

### 1.3 Lo que hay que aclarar (D-15)

"Un cliente" puede significar dos cosas. El diseño soporta ambas, pero hay que saber cuál entra primero:

| Interpretación | Qué se sincroniza | `ErpConexion.Ambito` | Veredicto |
|---|---|---|---|
| **A. El ERP de ORKIN** | Maestro de productos, precios, activos, tipo de cambio → los catálogos del cotizador | `1` | ✅ **Recomendada.** Es la que hace que el cotizador cotice con precios reales |
| **B. El ERP de cada cliente de ORKIN** | Ficha del cliente + inventario de equipos instalados en el sitio (trampas, lámparas, cebaderos) | `2` (con `ClienteId`) | 🔮 Tiene más sentido cuando exista el módulo de órdenes de trabajo (Fase 11) |

> **Aclaración importante:** el ERP de un cliente de ORKIN (un hotel, una fábrica) **no tiene los
> precios de venta de ORKIN ni el maestro de productos de ORKIN**. Sus productos son su comida, su
> maquinaria, sus repuestos. Sus datos nos sirven para el módulo de clientes y órdenes de trabajo,
> **no para el cotizador**. Por eso, aunque el pedido se formule como "el ERP del cliente", para el
> **cotizador** lo que hace falta es el ERP **de ORKIN**.

---

## 2. El modelo mental: un ETL, no un "sincronizador"

El cliente lo intuyó correctamente: esto es un ETL. Se modela con las cuatro piezas clásicas.

```
 ORIGEN  ──▶  TRANSFORMACIÓN  ──▶  VALIDACIÓN  ──▶  DESTINO
    │               │                  │              │
    │               │                  │              └─ dbo.Producto (upsert por CodigoExterno)
    │               │                  └─ reglas: obligatorio, rango, formato → int.ErpRechazo
    │               └─ campo→campo, transformaciones, filtros, enriquecimiento, dedup
    └─ tabla | vista | consulta SQL | API | archivo  (SOLO LECTURA)
                 (paginada, incremental, marca de agua)

                int.ErpFlujo = el "paquete" con su programación y sus dependencias

         ───────  y de aquí no sale nada hacia el ERP  ───────
```

Comparación con herramientas conocidas, para que sea fácil discutirlo:

| Pieza | SSIS | Talend / Airflow | dbt | **Este proyecto** |
|---|---|---|---|---|
| El "paquete" | Package | Job / DAG | Model | `int.ErpFlujo` |
| Origen | Data Flow Source | Input | `source()` | `int.ErpOrigen` |
| Transformación | Derived Column / Script Component | Transformation | `stg_` / `int_` / `fct_` | `int.ErpMapeoCampo` |
| Destino | Data Flow Destination | Output | `ref()` | `int.ErpDestino` |
| Reglas de calidad | Validation | Assertions | `tests` | `int.ErpValidacion` |
| Errores | Error Output | On failure | `dbt test` | `int.ErpRechazo` |
| Corridas | Job history | Airflow UI | `run_results` | `int.SincronizacionEjecucion` |
| Programación | Schedule | Cron | `dbt schedule` | `ErpFlujo.TipoDisparador` + `IntervaloSegundos` |
| Linaje | Data lineage | Lineage | `manifest.json` | `int.SincronizacionDetalle` |
| Estado antes de correr | — | — | — | `int.ErpSimulacion` (extras) |

**Lo que este diseño tiene y las herramientas de ETL no:**

- **Hash de contenido** en cada fila: si el ERP manda el mismo registro 2 000 veces, se escribe una
  sola. Es lo que hace viable la sincronización cada 30 segundos.
- **Conflict Detection por origen**: el ERP solo pisa lo que él trae; lo que alguien editó a mano en
  la web se respeta salvo que se marque el campo como "propiedad del ERP".
- **Simulación previa**: se ve exactamente qué se crearía, actualizaría o rechazaría, sin escribir.
- **Idempotencia por hash de evento**: un webhook repetido no procesa dos veces.
- **Auditoría de la propia integración**: cada cambio guarda valor anterior y nuevo.
- **Alta y baja como flujo de entrada**: la baja del ERP desactiva en el cotizador, con aviso si el
  producto está en uso, y **nunca borra** (ver §5).
- **Conexión de solo lectura garantizada por la base**: `CHECK (EsSoloLectura = 1)`. No se puede
  escribir en el ERP del cliente ni por error.

---

## 3. El flujo, paso a paso (ejemplo real: productos y precios del ERP)

### 3.1 Configuración (una sola vez, o cada vez que cambie el ERP)

**Paso 1 — Registrar la conexión** (`int.ErpConexion`)
- Nombre: `ERP ORKIN - Producción`
- Tipo: `SQL Server` (o el que sea)
- Servidor / base / esquema / usuario de **solo lectura**
- Se guarda `Probar conexión` → si responde, ✅
- Las credenciales van cifradas con Data Protection

**Paso 2 — Crear el flujo** (`int.ErpFlujo`)
- Nombre: `Catálogo de productos y precios`
- Entidad: `Producto`
- Disparo: `Híbrido` (evento + programado cada 5 min como red de seguridad)
- Extracción: `Incremental` por la columna `ART_FECHA_MOD`

**Paso 3 — Definir el origen** (`int.ErpOrigen`)

Se elige **una** de cuatro:

| Opción | Cuándo |
|---|---|
| **Tabla** | El ERP tiene un maestro de artículos listo |
| **Vista** | El ERP ya expone una vista limpia. Se referencia por nombre. ✅ la mejor si existe |
| **Consulta SQL** | Hay que construirla. Ejemplo de una consulta que ya normaliza en el origen: |
| **Endpoint API** | REST / OData / JSON-RPC |

```sql
-- Consulta de origen. Importante: normaliza AQUÍ, no en nuestra app.
SELECT  UPPER(LTRIM(RTRIM(A.ART_CODIGO)))               AS Codigo,
        LTRIM(RTRIM(A.ART_DESCRIPCION))                 AS Nombre,
        TRY_CONVERT(decimal(18,6),
             REPLACE(REPLACE(REPLACE(A.ART_COSTO,'C$',''),'$',''),',',''))  AS Costo,
        A.UNI_COD                                       AS Unidad,
        ISNULL(A.LIN_NOM,'SIN CATEGORÍA')                AS Categoria,
        A.ART_MARCA                                     AS Marca,
        A.ART_FECHA_MOD                                 AS FechaModificacion,
        A.ART_ACTIVO
FROM    dbo.ARTICULO A WITH (READUNCOMMITTED)
WHERE   A.ART_FECHA_MOD >= DATEADD(SECOND, -5, @MarcaAgua)   -- superficie de 5 s
ORDER BY A.ART_FECHA_MOD;
```

> **Recomendación: empujar la limpieza al origen cuando se pueda.** La lógica queda versionada en
> el ERP, que es donde vive el dato, y nosotros consumimos números ya limpios.

**Paso 4 — Mapear campos** (`int.ErpMapeoCampo`)

Grilla con una fila por campo, con ejemplo de la izquierda y la derecha:

| # | Campo origen | Campo destino | Transformaciones | Oblig. | Clave | Ejemplo entrada → salida |
|---|---|---|---|---|---|---|
| 1 | `Codigo` | `Codigo` | `TRIM`,`UPPER` | ✅ | ✅ | `" prod-01 "` → `"PROD-01"` |
| 2 | `Nombre` | `Nombre` | `TRIM`,`NORMALIZAR_TEXTO` | ✅ | | `"Ratón  A"` → `"RATON A"` |
| 3 | `Costo` | `CostoActual` | `NUMERO:6`,`DECIMAL` | ✅ | | `"C$ 1,234.50"` → `1234.500000` |
| 4 | `Precio` | `PrecioVentaActual` | `NUMERO:6` | | | `"$312.00"` → `312.000000` |
| 5 | `Unidad` | `UnidadMedida` | `UPPER`,`UNIDAD_MEDIDA(ML,LT,L→LT;UND,U,PZA→UND)` | ✅ | | `"litros"` → `LT` |
| 6 | `Categoria` | `Categoria` | `TRIM`,`UPPER` | | | `"insect "` → `"INSECT"` (crea si no existe) |
| 7 | `Marca` | `Marca` | `TRIM` | | | |
| 8 | `Activo` | `EsActivo` | `BOOLEANO` | | | `"S"` → `true` |

> **Enriquecimiento** (campo 5 y 6): el destino puede ser otra tabla (`UnidadMedida`,
> `CategoriaProducto`). El flujo la busca por código y, si no existe, la crea con
> `ValorMaestroPorDefecto`. Por eso `int.ErpDependencia` dice que **unidades y categorías deben
> correr antes que productos**: así el enriquecimiento usa maestros ya sembrados y no duplica.

**Paso 5 — Validar** (`int.ErpValidacion`)

Se siembran solas (ver `BASE-DE-DATOS.md` §7.6), y se pueden agregar: `Costo <= 1000000`,
`Nombre` no puede empezar con "PRUEBA", `Codigo` debe seguir el patrón `^[A-Z0-9-]{3,30}$`.

**Paso 6 — Simular** (`int.ErpSimulacion`) ← **el paso que másusta de todo el diseño**

Botón **"Simular"**: lee 100 filas, transforma, valida y devuelve una grilla:

| Código origen | Código destino | Acción | Precio anterior | Precio nuevo | Validación | Motivo del rechazo |
|---|---|---|---|---|---|---|
| `PROD-01` | `PROD-01` | 🆕 Crear | — | `1234.50` | ✅ | |
| `PROD-02` | `PROD-02` | 🔄 Actualizar | `1200.00` | `1250.00` | ✅ | `Costo cambió +4.2%` |
| `PROD-03` | — | ❌ Rechazar | — | — | ❌ | `CostoActual >= 0` (recibido `-5`) |
| `PROD-04` | `PROD-04` | ⏭️ Sin cambio | `980.00` | `980.00` | ✅ | |

Nada se escribe. Se revisa, se corrige el mapeo, se simula otra vez, y solo entonces **Activar**.

**Paso 7 — Activar** (`Estado = 3`)

### 3.2 Ejecución (cada vez que se dispara)

```
1. Extraer   → SELECT paginado, filtrado por MarcaAgua + SuperficieDeWatermark
                 (el ERP se abre en solo lectura; si se intenta un INSERT, el permiso falla)
2. Transformar → por registro: trim, normalizar, número, fecha, booleano, deduplicar
3. Validar   → reglas de int.ErpValidacion; lo que falla va a int.ErpRechazo
4. Comparar  → SHA-256 vs HashContenido de la fila existente
                 ├─ no existe  → INSERT  (RegistrosCreados++)
                 ├─ cambió     → UPDATE  (RegistrosActualizados++) + escribe PrecioProducto
                 ├─ igual      → nada   (RegistrosSinCambio++)
                 └─ el ERP lo dio de baja → UPDATE EsActivo = 0  (RegistrosDesactivados++)
                    + aviso si el producto está en cotizaciones abiertas
5. Confirmar → si Estado = 2 (Correcta): MarcaAgua = MarcaAguaNueva
               si Estado = 4 (Fallida): NO se avanza la MarcaAgua → se reintenta todo
6. Registrar → int.SincronizacionDetalle por cada cambio
               int.SincronizacionEjecucion con los contadores
```

**Los pasos 4 y 5 son lo que hace que 30 segundos sea barato:** en una sincronización típica de
400 productos donde cambió uno, se lee 1 fila y se escribe 1. Las otras 399 no se tocan.

### 3.3 La marca de agua y el problema de los registros perdidos

El error clásico de la sincronización incremental:

> El ERP actualiza el producto `PROD-01` a las `10:00:00.500`. La corrida de las `10:00:05` lee
> hasta `10:00:05` y guarda esa marca. La corrida de las `10:05:00` lee "cambios desde `10:00:05`"
> y **el cambio de las `10:00:00.500` se perdió para siempre**.

Solución implementada — `SuperficieDeWatermark` (default 5 s):

```sql
WHERE A.ART_FECHA_MOD >= DATEADD(SECOND, -5, @MarcaAgua)   -- superpone las ventanas
```
y al confirmar se guarda `MAX(ART_FECHA_MOD) - 5 s`, de modo que siempre haya solape. **Se releen
algunas filas, pero nunca se pierde ninguna.** Como la comparación es por hash, releer no causa
escrituras duplicadas.

Alternativas si el ERP tiene **Change Tracking** o **CDC** activos: `ModoExtraccion = 5`, que es más
eficiente y exacto. Se usa si el cliente lo tiene habilitado (requiere permiso para crear objetos en
el ERP del cliente, y un DBA que lo autorice).

---

## 4. Frecuencia: qué se recomienda

El cliente preguntó por 30 segundos o 1 minuto. La recomendación tiene tres partes.

### 4.1 El costo real de 30 segundos

| Métrica | 30 s | 1 min | 5 min | 15 min |
|---|---|---|---|---|
| Ejecuciones/día por flujo | 2 880 | 1 440 | 288 | 96 |
| Con 6 flujos | 17 280 | 8 640 | 1 728 | 576 |
| Consultas/día contra el ERP | 17 280 | 8 640 | 1 728 | 576 |
| Consultas/día **útiles** (incremental) | ~10–50 | ~10–50 | ~10–50 | ~10–50 |

Las consultas inútiles (las que no devuelven nada) son **baratas en nuestra BD y moderadas en el ERP**.
Con modo incremental + índice en la columna de fecha, cada una es un seek de milisegundos. El
riesgo no es el volumen: es la **percepción del DBA del cliente**, que ve su servidor consultándose
cada 30 segundos. Eso se negocia, no se ignora.

### 4.2 La respuesta corta

> **30 segundos no aporta valor de negocio en este caso, y 5 minutos tampoco se queda corto.
> Lo que aporta valor es el evento, no el sondeo.**

Un ERP de control de plagas no cambia precios 2 880 veces al día. Cambia:

| Dato | Frecuencia real de cambio | Latencia que importa |
|---|---|---|
| **Tipo de cambio** | 1 vez al día | Minutos. El valor debe estar disponible **antes de las 8 a. m.** |
| **Precios de productos** | 2–10 veces al día | Horas. El vendedor necesita ver el precio al día |
| **Inventario de equipos** | Al cerrar cada visita | Horas |
| **Alta de un producto nuevo** | Rara | Días |

En **ningún** caso hace falta inmediatez de 30 segundos. Lo que hace falta es **no quedarse
con un precio de hace tres días**, y eso lo resuelven 5 minutos holgadamente.

### 4.3 La recomendación por escalones

| Nivel | Configuración | Latencia | Cuándo usarlo |
|---|---|---|---|
| ✅ **Recomendado por defecto** | **Híbrido**: evento (webhook) + programado cada **5 min** | 2–5 s si hay evento; ≤ 5 min si no | Catálogos de productos, activos, servicios |
| **Alta frecuencia** | Programado cada **60 s** + evento | ≤ 1 min | Si el cliente exige "ver el precio al instante". Justificable: son 1 440 consultas/día, insignificantes para el ERP |
| **Frecuencia alta** | Programado cada **30 s** + evento | ≤ 30 s | **Solo** si estájustificado por escrito. Se activa con advertencia en la UI |
| **Bajo demanda** | Manual | — | Cuando se configura por primera vez, o en semana Santa |
| **Nunca** | Cada 1–5 min | — | No. Es la peor opción: mucha carga, y aun así no es inmediatez |

> **Nunca usar el modo `Completa` (full) con programación.** Solo se permite una vez, al activar el
> flujo. Después, el flujo cambia a `Incremental`. Una carga completa cada 5 minutos sobre 400
> productos son 115 200 escrituras-potenciales al día, y no aporta nada.

### 4.4 El orden correcto: evento primero, sondeo como red

```
        ┌──────────────────────────────────────────┐
        │  EVENTO (webhook del ERP)                │
        │  → despierta el flujo en 2–5 s           │  ← inmediatez real
        │  → idempotente por HashUnico             │
        └────────────────┬─────────────────────────┘
                         │
                         ▼
        ┌──────────────────────────────────────────┐
        │  SONDEO PROGRAMADO (cada 5 min)          │
        │  → red de seguridad                      │  ← por si el webhook falló
        │  → cubre el caso "el ERP no notifica"     │
        └──────────────────────────────────────────┘
```

**El sondeo de 5 minutos no es el plan A**: es la red que garantiza que, aunque el webhook se haya
caído, nada se queda más de 5 minutos desactualizado. Esta es la razón por la que **la latencia
real es de 5 minutos, no de 5 minutos en el peor caso con 30 segundos de sondeo**.

### 4.5 Cómo conseguir el evento, según el ERP

| ERP / base | ¿Notifica cambios? | Cómo | Dificultad |
|---|---|---|---|
| **SQL Server** | Sí | `CHANGE_TRACKING` + `CHANGE_TRACKING_CURRENTVERSION()` en la consulta, o una vista con `SYS.CHANGETABLE()`. En la opción avanzada, un **trigger `FOR UPDATE` + Service Broker** que notifica a la API | 🟢 Media (hablar con el DBA) |
| **PostgreSQL** | Sí | `LISTEN`/`NOTIFY` con triggers, o consultar `xmin` (xmin como marca de agua) | 🟢 Media |
| **MySQL / MariaDB** | No nativo | Trigger a una tabla de cola + sondeo de esa cola, o binlog | 🟡 Media |
| **Oracle** | Sí | `ORA_ROWSCN` como marca de agua, o `CDC` | 🟡 Media |
| **Odoo** | Sí | Longpolling de `write_date` (`?domain=[["write_date",">",...]]&context={"order":"write_date asc"}`) | 🟢 Baja |
| **SAP** | Sí | Change pointers (CDC en tablas estándar) o entradas IDOC en una tabla de staging | 🟡 Media-Alta |
| **Dynamics / Contpaq** | Depende | Webhook nativo o cola SQL | 🟡 Media |
| **API REST común** | A veces | Campo `updated_at` en la respuesta → sondeo incremental cada 60 s | 🟢 Baja |
| **Ninguna** | — | Solo sondeo. Entonces **60 s** es el máximo razonable | 🟢 — |

> **El sondeo incremental sobre `updated_at` con un filtro `>=` ya cubre el 90 % de los casos**,
> y no requiere tocar el ERP del cliente en absoluto. Eso es una ventaja grande frente a "exigir
> Change Tracking": **nadie tiene que concedernos permisos de escritura en su base de producción.**

### 4.6 Protección contra abusive polling

| Regla | Valor |
|---|---|
| `IntervaloSegundos` mínimo | `10` (`CHECK` en la base) |
| Intervalos permitidos en la UI | 30 s · 1 min · 5 min · 15 min · 1 h · diario |
| `DesfaseSegundos` (jitter) | 0–30 s, asignado al crear el flujo, para que todos los flujos no peguen al ERP en `:00` y `:30` |
| Tope por corrida | `MaximoRegistrosPorCorrida` (default `NULL`) |
| Reclamo atómico | `UPDLOCK, ROWLOCK` → dos instancias nunca duplican trabajo |
| Falla 5 veces seguidas | `FallosConsecutivos` → auto-pausa el flujo y crea `int.ErpAlerta`. **Nunca un bucle infinito golpeando el ERP** |
| Respetar la ventana del ERP | `DiasSemana` + `HoraInicioUtc` / `HoraFinUtc` + `SoloFueraDeVentana`. El ERP de un cliente puede no permitir conexiones a las 2 a. m. |
| Respetar el ritmo del ERP | `LoteRegistros` y `TiempoEsperaSegundos` por conexión, configurables por el DBA del cliente |

---

## 5. Alta y baja de productos — el caso que más se usa

> El cliente lo nombró explícitamente: *"que alta y baja de productos"*. Junto con el cambio de
> precio, es lo que hace que valga la pena tener la integración.

### 5.1 Las cuatro situaciones del día a día

| Situación | Qué hace el flujo | Contador |
|---|---|---|
| El ERP tiene un producto que nosotros no | `INSERT` en `dbo.Producto` con `EsActivo = 1`, `OrigenDato = 2 (ERP)`, `CodigoExterno = <código del ERP>` | `RegistrosCreados++` |
| El ERP cambió el precio o el costo | `UPDATE` de `PrecioVentaActual` / `CostoActual` **+ `INSERT` en `dbo.PrecioProducto`** (historial con fecha) | `RegistrosActualizados++` |
| El ERP **dio de baja** un producto | `UPDATE … SET EsActivo = 0, FechaUltimaSincronizacionUtc = @ahora` | `RegistrosDesactivados++` |
| El ERP lo **reactivó** | `UPDATE … SET EsActivo = 1` | `RegistrosActualizados++` |

### 5.2 Por qué la baja **nunca** borra

Un producto dado de baja en el ERP puede aparecer en una cotización **aprobada** de hace tres meses,
que es un documento con valor comercial y a veces legal. Si lo borramos, esa cotización pierde su
insumo y el PDF histórico deja de cuadrar. Por eso el diseño tiene tres capas de protección:

| Capa | Mecanismo | Resultado |
|---|---|---|
| 1. **No se borra** | `PoliticaBaja` por defecto = `2` (marcar inactivo). El borrado físico (`4`) existe pero exige `BajaFisicaConfirmada = 1`, que nadie marca solo | El producto sigue existiendo en `dbo.Producto` para siempre |
| 2. **La cotización no depende del producto** | `dbo.CotizacionDetalle` guarda un **snapshot**: nombre, unidad, costo y precio del momento en que se cotizó | Un cambio de precio mañana **no altera** una cotización de ayer. Es lo correcto: el presupuesto que se le dio al cliente debe seguir valiendo lo que decía |
| 3. **Aviso antes de desactivar** | `AvisarSiEstaEnUso = 1`: antes de poner `EsActivo = 0`, se revisa si el producto está en cotizaciones `Borrador` o `Aprobada`; si lo está, se crea un `int.ErpAlerta` de severidad `2` | Alguien se entera **antes** de que un vendedor cotice un producto que ya no existe en el ERP |

### 5.3 Cómo se ve en la UI

Un producto desactivado por el ERP se muestra en el catálogo con una etiqueta gris
`Inactivo (ERP)` y un tooltip con la fecha y el motivo. En el formulario de cotización **aparece al
final de la lista, no al principio**, y con un aviso si tiene cotizaciones abiertas. No se esconde:
es más importante que el vendedor sepa que existe y que está dado de baja, que que no lo vea.

### 5.4 Qué pasa si el ERP "olvida" un producto

Modo `Completa` (full): si el ERP trae 400 productos y antes traía 401, el que falta **no se toca**
(`PoliticaBaja = 1`, "nada"). Razón: un ERP con filtro de permisos puede devolver 400 porque al
usuario le faltan 5 registros, no porque se hayan dado de baja. Borrar por ausencia es un
error clásico de integración.

La única forma de dar de baja algo que el ERP dejó de traer es que el ERP **diga explícitamente**
que está inactivo (`CampoBaja` = `ART_ACTIVO = 0`). Si no trae ese campo, no hay baja automática,
y la UI lo indica: *"Este origen no trae indicador de baja; los productos solo se desactivan si el
ERP lo dice."*

---

## 6. Conflictos: qué pasa si alguien editó en la web lo que vino del ERP

Este es el problema que nadie menciona y que rompe las integraciones en producción. Cinco políticas,
seleccionables **por campo** si hace falta:

| Política | Cuándo |
|---|---|
| **Gana el ERP** | Datos que ORKIN no edita nunca: `CostoActual`, `PrecioVentaActual`, códigos. ← **recomendado** |
| **Gana la web** | Datos que alguien carga a mano aunque el ERP los traiga: p.ej. `PrecioProducto` de un acuerdo especial |
| **Gana el más reciente** | Se compara `FechaModificacion` del ERP contra `UpdatedAt` de nuestra fila. **Requiere que las dos fuentes tengan reloj confiable** |
| **Bloquear: revisión manual** | Se detecta el conflicto, no se resuelve solo, y aparece en una bandeja para que alguien decida. Es la más segura para datos sensibles |
| **Solo crear** | El flujo solo crea registros nuevos; nunca modifica los existentes |

**Detección de conflicto con precisión:**

```
HashContenido = SHA-256( CodigoExterno
                        + CostoActual + PrecioVentaActual
                        + Nombre + UnidadMedida + EsActivo )
```

Cuando el hash remoto **es igual** al almacenado → nada cambió → no se escribe. Cuando es distinto,
se comparan **campo por campo** contra `HashAnterior` guardado en `dbo.PrecioProducto` para saber
**quién** lo cambió. La columna que cambió en los dos lados a la vez es un conflicto real y se
reporta.

> **El caso de la web, en una integración de solo lectura, es más simple de lo que parece:** casi
> todo lo que el ERP trae es de propiedad del ERP. La excepción real son los campos que el cotizador
> calcula y que **no** vienen del ERP: margen aplicado, tipo de cambio usado, precios unitarios
> ajustados. Esos nunca se sobreescriben porque no vienen en el mapeo.

---

## 7. Lo que hay que construir (Fase 12, desglosada)

| # | Tarea | Entregable | Depende de |
|---|---|---|---|
| 12.1 | Confirmar **D-15** (ERP de ORKIN o de cada cliente) y el ERP concreto | Alcance cerrado | Cliente |
| 12.2 | Esquema `int` completo: `ErpFlujo`, `ErpOrigen`, `ErpMapeoCampo`, `ErpValidacion`, `ErpDestino`, `ErpRechazo`, `ErpDependencia` | Migración | 12.1 |
| 12.3 | `ErpConexion` + CRUD + cifrado (`Data Protection`) + "probar conexión". **Con `EsSoloLectura` fijo en `1`** | Pantalla de conexiones | 12.2 |
| 12.4 | **`IErpConector`**: solo `LeerPagina(entidad, cursor)`. **Sin `Escribir`** | Abstracción | 12.3 |
| 12.5 | `SqlServerErpConector` (el primero; los demás son copias del mismo patrón) | | 12.4 |
| 12.6 | Motor de transformación: `TransformacionPipeline` con el catálogo de ~20 operaciones | Código puro + tests | 12.2 |
| 12.7 | Motor de validación + `int.ErpRechazo` + bandeja de rechazos en la UI | Nada falla en silencio | 12.6 |
| 12.8 | Cargador: *upsert* por clave, comparación por `HashContenido`, historial de `PrecioProducto`, y **alta/baja** con el aviso de "en uso" | | 12.7 |
| 12.9 | `ErpOrquestador`: extrae → transforma → valida → carga → registra | | 12.5–12.8 |
| 12.10 | **`int.ErpSimulacion`**: el botón "Simular" con grilla de resultados | El paso que evita arruinar un catálogo | 12.9 |
| 12.11 | `ErpSincronizacionBackgroundService`: scheduler + disparo por evento + reclamo atómico | "En vivo" | 12.9 |
| 12.12 | `WebhookController` (público, por token, con HMAC) → `int.EventoRecibido` | Inmediatez real | 12.11 |
| 12.13 | `int.ErpDependencia`: orden de ejecución entre flujos (TC y categorías antes que precios) | | 12.11 |
| 12.14 | Pantalla de monitoreo: corridas, contadores, rechazos, alertas, detalle de cambios | Operabilidad | 12.11 |
| 12.15 | Alertas por correo: conexión fallida, 5 fallos seguidos, muchos rechazos, producto desactivado en uso | | 12.14 |
| 12.16 | Conectores adicionales (PostgreSQL, REST, Odoo, MySQL, archivo) | Uno por ERP real | 12.5 |
| 12.17 | Pruebas con un ERP de mentira (contenedor) + pruebas de integración de punta a punta, incluida la prueba de que **no** se puede escribir en el ERP | | 12.9 |
| 12.18 | Manual de operación: qué revisar cuando algo falla | `docs/DESPLIEGUE.md` | 12.14 |

> **No hay tarea de escritura al ERP.** Esa es la decisión D-19.

---

## 8. Riesgos y cómo se mitigan

| Riesgo | Impacto | Mitigación |
|---|---|---|
| El ERP **cae** o está lento | La sincronización falla | `TiempoEsperaSegundos`, reintentos con backoff, el cotizador **sigue funcionando con los datos del último sync**. La UI muestra "última sincronización: hace 2 h" |
| La consulta escrita por un usuario **rompe** la base del cliente | Catastrofe | Solo `SELECT`, una sentencia, lista blanca de palabras, usuario de solo lectura, validación al guardar y en cada ejecución, y todo queda auditado |
| El ERP **manda datos basura** | Costo mal calculado | Validaciones obligatorias (`Costo >= 0`, `VidaUtilMeses > 0`, `TipoCambio > 0`), simulación previa, y la opción de **rechazar** en vez de escribir |
| Alguien **edita en la web** un precio que trae el ERP | Se pierde el cambio | `HashContenido` detecta el conflicto y se notifica. La opción "bloquear" lo lleva a una bandeja de revisión |
| Un cambio en el ERP **del cliente** rompe la integración | La sincronización deja de servir | **La configuración del mapeo está versionada en nuestra base** (`SincronizacionDetalle` guarda la ejecución completa). Se puede reconstruir el mapeo |
| El ERP da de baja un producto que se está usando | Cotización sin insumo | `EsActivo = 0`, **sin borrar**, con alerta antes de desactivar si tiene cotizaciones abiertas, y snapshot en cada cotización |
| El ERP **deja de traer** un producto | Catálogo con huecos falsos | Por defecto no se toca nada (`PoliticaBaja = 1`). La baja solo ocurre si el ERP **lo dice explícitamente** |
| El cliente **cambia de ERP** | Hay que rehacer todo | `ErpConexion` es una fila más. El destino (`dbo.*`) no cambia. Se crea otra conexión y se apunta el flujo a la nueva |
| El **volumen** del ERP es enorme | La sincronización tarda | Modo incremental, paginación keyset, `MaximoRegistrosPorCorrida`, y si hace falta, conmutar a `Full` los fines de semana |
| Duplicados en el ERP | Catálogo duplicado | `ClaveNegocio` + `DeduplicarPor`; los duplicados van a `int.ErpRechazo` con motivo "ya existe" |
| El ERP da permiso de escritura y alguien lo usa por error | Daño en producción del cliente | `CHECK (EsSoloLectura = 1)`, `IErpConector` sin métodos de escritura, y el usuario SQL que pedimos es de solo lectura. **Tres capas, ninguna de ellas una convención** |

---

## 8. Lo que se le puede mostrar al cliente

Pantalla de **Integraciones → ERP** (resumen del diseño de UI):

```
┌─ Conexiones ────────────────────────────────────────────────────────┐
│ ● ERP ORKIN - Producción        SQL Server   Activa   hace 40 s     │
│   Solo lectura · usuario erp_lectura@prod · 0 escrituras           │
│ ○ ERP Pruebas                   PostgreSQL   EnPrueba nunca         │
└─────────────────────────────────────────────────────────────────────┘

┌─ Flujos ────────────────────────────────────────────────────────────┐
│ Catálogo de productos y precios                                     │
│   Productos.VW_ARTICULOS_LIMPIA  →  dbo.Producto   Híbrido · 5 min  │
│   Incremental por FechaMod · Política de baja: desactivar          │
│   Última: 1 234 leídos · 7 cambiados · 2 dados de baja · 1 rech.    │
│                                                                    │
│ Tipo de cambio                                                      │
│   BCN API  →  dbo.TipoCambio                Programado · 1 h       │
│   Última: 1 leído · 1 creado                   hace 22 min          │
└─────────────────────────────────────────────────────────────────────┘

┌─ Pendientes de revisar ─────────────────────────────────────────────┐
│ ⚠ 3 rechazos en "Catálogo de productos y precios"                │
│   PROD-03  Costo recibido -5 (regla: Costo >= 0)      [Ver] [Resolver]
│                                                                    │
│ ⚠ 2 productos desactivados por el ERP con cotizaciones abiertas    │
│   RATA-GEL  2 cotizaciones borrador      [Ver] [Reactivar]          │
└─────────────────────────────────────────────────────────────────────┘
```

Cada flujo se abre en un **asistente de 6 pasos**:
`1 Conexión → 2 Origen → 3 Mapeo de campos → 4 Validaciones → 5 Simular → 6 Activar`.
Nunca se puede activar un flujo sin haber pasado por **Simular**.

> **La línea "Solo lectura · usuario erp_lectura@prod · 0 escrituras"** aparece en la lista de
> conexiones de forma permanente. Es la garantía, visible, de que el cotizador nunca toca la base
> del cliente. Cuando en el futuro se discuta la orden de compra (D-19), esta línea es lo primero
> que cambia.

---

## 9. Lo que ya está construido (2026-09-29)

La primera versión es **funcional de punta a punta** y está probada contra una base de datos
ficticia que simula el ERP de un cliente. Alcanza el recorrido completo —conectar, leer una vista,
transformar, cargar, omitir lo que no cambió y dejar bitácora— con un solo catálogo (`PRODUCTOS`)
y un solo motor (SQL Server).

### 9.1 Qué quedó fuera de esta versión, y por qué

| Fuera | Por qué ahora |
|---|---|
| `ErpOrigen` / `ErpMapeoCampo` como tablas (12.2) | El origen y el mapeo viven **en la misma fila** de `int.ErpFlujo` (`Origen` + `MapeoJson`). Con un origen por flujo no hay nada que normalizar en tablas separadas, y partir el mapeo en filas obliga a unite al leerlo. Si aparece el segundo origen por flujo, se extrae. |
| `IErpConector` / `SqlServerErpConector` (12.4–12.5) | Hoy hay un `ILectorOrigen` (`LectorSqlServer`) que lee y devuelve filas. La abstracción de conector se necesita cuando el segundo ERP sea de otra tecnología; con uno solo sería una interfaz con un solo implementador. |
| Simulación previa obligatoria (12.10) | Falta. Es la pieza que más urge: hoy el flujo escribe a medida que lee, y una vista mal armada alcanza a cargar el catálogo antes de que nadie la mire. |
| Programación automática (12.11–12.12) | Falta. Hoy la sincronización se dispara a mano desde la pantalla. El motor de carga ya sabe ser idempotente (hash), así que correr cada 5 minutos no es peligroso. |
| Los otros catálogos (12.x) | Hay cargador de `PRODUCTOS` únicamente. Los demás siguen el mismo camino: un `CargadorX` más un `MapeoPorDefecto`. |
| Alta/baja de productos (12.8, §5) | El cargador actual hace *upsert*, no baja. Una baja automática sin aviso borra insumos de cotizaciones abiertas. |
| Alertas por correo (12.15) | Falta. Hoy el que se entera de un fallo es quien mira la bitácora. |

### 9.2 Lo que sí quedó, y cómo está resuelto

**El origen es una vista, tabla o consulta — no una tabla.**

Es la decisión de diseño que más importa, y la que el cliente va a notar primero. No se le pide
a nadie que reorganice su base: se le pide **una vista** que entregue los datos como los espera la
plataforma. Si el dato del cliente vive repartido en `ARTICULO` + `UNIDAD` + `GRUPO` con nombres de
columna propios, la vista las une y normaliza. El cargador no lo sabe y no lo necesita.

La demo de `scripts/demo-cliente/` es exactamente ese caso: el ERP ficticio tiene esas tres
tablas, con unidades suyas (`ml`, `Litro`, `H.H`, `lb`, `Bolsa`, `Rollo`, `Yarda`, `Par`), y el
contrato es la vista `dbo.vw_ProductosOrkin`, que las une y traduce los códigos de unidad a los
nuestros (`ML`, `LT`, `H.H`, `LB`, `BOLSA`, `ROLLO`, `YARDA`, `PAR`).

> **La normalización va en la vista, no en el cargador.** Un cargador que sabe que `Litro` es
> `LT` tiene que mantener una lista de sinónimos por cliente, y esa lista se desactualiza en
> silencio: el día que el ERP agrega una unidad, la fila se rechaza sin que nadie se entere.
> En la vista, el cambio es del DBA, que es quien sabe.

**Mapeo por identidad cuando no hay `MapeoJson`.**

Si el flujo no trae mapeo, se usan los nombres de campo que ya espera el cargador (`Codigo`,
`Nombre`, `CategoriaCodigo`, `UnidadMedidaCodigo`, `CostoActual`, `PrecioVentaActual`,
`VidaUtilMeses`). Es el caso más común: la vista ya se construyó con esos nombres.

**La búsqueda de existentes está acotada por conexión.**

`CargadorProductos` busca por `Codigo` **dentro de la conexión que está corriendo**. Sin ese
`WHERE`, dos clientes con el mismo código (`001 - Aceite`) se pisan entre ellos y el catálogo
termina con el precio del último que sincronizó.

**Adopción de filas huérfanas.**

`dbo.Producto.Codigo` es único a nivel de tabla, pero el ERP no lo sabe. Un producto que entró
por el Excel y después llega del ERP con el mismo código no se inserta: se **adopta**. La adopción
se cuenta como *actualizado*, no como insertado, y se escribe siempre (sin mirar el hash) para no
dejar la conexión sin reclamar sus propias filas.

> Sin esta adopción el `INSERT` revienta con `UX_Producto_Codigo`, y el mensaje dice "violación de
> clave", que no dice "el producto ya existe desde otro origen". Es exactamente el escenario real:
> el Excel es el histórico, el ERP es el presente, y hay una ventana de solapamiento.

**La contraseña nunca sale y nunca se ve.**

`int.ErpConexion.ContrasenaCifrada` es `varbinary(max)` cifrado con **ASP.NET Core Data
Protection**, con propósito `Orkin.Cotizador.ErpConexion.Contrasena.v1`. El DTO de respuesta lleva
`TieneContrasena: bool` y jamás la contraseña. Si el juego de claves rota, el descifrado devuelve
`null` en vez de lanzar: la conexión queda marcada como "sin credenciales" y hay que
reconfigurarla, sin que el error diga "clave inválida" cuando el problema era otro.

> En `docker-compose.yml` las claves viven en el volumen `orkin-dp-keys`. Sin eso, cada
> `up --build` regenera el juego y todas las conexiones guardadas dejan de descifrar. En
> producción hay que cifrarlo y respaldarlo: perder ese volumen es perder el acceso de todos los
> clientes conectados.

**Solo lectura, en tres capas y ninguna por convención.**

`EsSoloLectura` tiene `CHECK (EsSoloLectura = 1)` en la base, así que ni una sentencia escrita a
mano desde SSMS puede ponerlo en `0`. El lector no tiene métodos de escritura. Y la cuenta que se
pide al cliente es de solo lectura.

**Bitácora de cada corrida.**

`int.SincronizacionEjecucion` guarda inicio, fin, duración, estado y los cuatro contadores (leídos,
creados, actualizados, sin cambio, rechazados), más el mensaje de error cuando falla. La lista de
flujos muestra el resultado de la última corrida; la pantalla de orígenes muestra el historial.

**La idempotencia está probada con `HashContenido`.**

Correr el mismo flujo dos veces seguidas da `111 insertados` y después `111 omitidos`, sin tocar
una sola fila. Es lo que permite programar la sincronización sin miedo cuando llegue (12.11).

### 9.3 Cómo se prueba, de punta a punta

Con Docker levantado y la base del cliente ficticio creada:

```powershell
# 1. Esquema del ERP ficticio (ARTICULO + UNIDAD + GRUPO + la vista contrato)
Get-Content scripts\demo-cliente\01-esquema.sql -Raw |
  docker exec -i orkin-db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "OrkinDev_2026" -C -b

# 2. Datos desde el Excel de ORKIN (111 artículos, 1309 días de TC, 11 servicios)
dotnet run --project tools\Orkin.DemoCliente.CargarDatos -- "Cotizador Original - Agosto 31 2026.xlsm"

# 3. API arriba (aplica migraciones y siembra al arrancar)
docker compose up -d --build
```

Y en `http://localhost:59793/catalogos/origenes`, con `admin@orkin.local`:

1. **Conexión** → servidor `db`, puerto `1433`, base `OrkinClienteDemo`, usuario `sa`.
2. **Probar** → debe decir "Conexión correcta" y el tiempo que tardó.
3. **Flujo** → catálogo `PRODUCTOS`, origen `dbo.vw_ProductosOrkin`, mapeo vacío.
4. **Ejecutar** → `111` nuevos.
5. **Ejecutar otra vez** → `111` sin cambio.
6. **Historial** → las dos corridas, con duración y contadores.

> El paso 2 tiene que ir contra `OrkinClienteDemo` y no contra la base de la plataforma: si se
> corre con la cadena de la plataforma, el cargador se está leyendo a sí mismo.

### 9.4 El permiso `CATALOGOS.ORIGENES`

Abrir una conexión a la base de datos de un cliente **no** es lo mismo que corregir un precio de
un producto. El primero puede leer catálogos completos de un ERP y cambiar de dónde viene el
dato; el segundo no. Por eso es un permiso aparte, en vez de colgar de `CATALOGOS.EDITAR`:
con un permiso solo, o se le daba a todo el que corrige precios —y entonces la gente de ventas
veía la lista de servidores de los clientes— o no se le daba a nadie y había que abrir la puerta
completa.

---

*Documento en revisión. Los cambios de última hora en el modelo de datos van también en
`BASE-DE-DATOS.md` §7 y en el README §9 (ADR-015).*
