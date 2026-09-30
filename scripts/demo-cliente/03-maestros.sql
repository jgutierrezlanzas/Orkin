-- ═════════════════════════════════════════════════════════════════════════════
--  Vistas de contrato para los catálogos maestros y el tipo de cambio.
--
--  Igual que vw_ProductosOrkin y vw_ClientesOrkin: el ERP ficticio guarda los datos con
--  sus nombres y formatos propios, y estas vistas son lo que el ETL consume — el cliente
--  no cambia NADA de sus tablas; el contrato es la vista.
--
--  Nota: el histórico de tipos de cambio del ERP demo llega hasta 2024-06-30 (así venía
--  el Excel). Para que la plataforma pueda cotizar con "el TC de hoy", la vista también
--  reparte el último valor conocido hacia adelante hasta hoy. El motor de cotización ya
--  hace exactamente eso (RN-09: último valor <= fecha), así que las dos cosas cuadran.
-- ═════════════════════════════════════════════════════════════════════════════

USE OrkinClienteDemo;
GO

-- ── Unidades de medida ────────────────────────────────────────────────────────
-- El ERP tiene su propio maestro (ml, Litro, H.H, und, Bolsa...). La abreviatura es
-- el propio código y todas las unidades del demo permiten decimales salvo las
-- contables obvias.
IF OBJECT_ID('dbo.vw_UnidadesOrkin', 'V') IS NOT NULL DROP VIEW dbo.vw_UnidadesOrkin;
GO
CREATE VIEW dbo.vw_UnidadesOrkin
AS
SELECT
    UNI_CODIGO        AS Codigo,
    UNI_NOMBRE        AS Nombre,
    UNI_CODIGO        AS Abreviatura,
    CASE WHEN UNI_CODIGO IN ('und', 'UNIDAD', 'Par', 'par', 'Sitio', 'Sitio')
         THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS PermiteDecimales,
    ROW_NUMBER() OVER (ORDER BY UNI_CODIGO) AS Orden
FROM dbo.UNIDAD
WHERE UNI_ACTIVO = 1;
GO

-- ── Categorías de producto (los grupos del ERP) ──────────────────────────────
IF OBJECT_ID('dbo.vw_CategoriasOrkin', 'V') IS NOT NULL DROP VIEW dbo.vw_CategoriasOrkin;
GO
CREATE VIEW dbo.vw_CategoriasOrkin
AS
SELECT
    GRU_CODIGO        AS Codigo,
    GRU_NOMBRE        AS Nombre,
    NULL              AS Descripcion,
    ROW_NUMBER() OVER (ORDER BY GRU_CODIGO) AS Orden
FROM dbo.GRUPO
WHERE GRU_ACTIVO = 1;
GO

-- ── Tipos de servicio ────────────────────────────────────────────────────────
-- La unidad de cobro es la traducción de lo que el ERP sabe: los servicios se
-- cobran por visita (unidad) en este demo.
IF OBJECT_ID('dbo.vw_TiposServicioOrkin', 'V') IS NOT NULL DROP VIEW dbo.vw_TiposServicioOrkin;
GO
CREATE VIEW dbo.vw_TiposServicioOrkin
AS
SELECT
    SVC_CODIGO        AS Codigo,
    SVC_NOMBRE        AS Nombre,
    NULL              AS Descripcion,
    'und'             AS UnidadMedidaCodigo,
    CAST(0 AS bit)    AS EsResidencialPorDefecto,
    CAST(0 AS bit)    AS EsEmpresarialPorDefecto,
    ROW_NUMBER() OVER (ORDER BY SVC_CODIGO) AS Orden
FROM dbo.TIPO_SERVICIO
WHERE SVC_ACTIVO = 1;
GO

-- ── Tipo de cambio ───────────────────────────────────────────────────────────
-- El histórico crudo del ERP (1.309 días, 2020-01-01 a 2024-06-30) más la
-- extensión del último valor conocido hasta hoy, para que cotizar "hoy" tenga TC.
IF OBJECT_ID('dbo.vw_TiposCambioOrkin', 'V') IS NOT NULL DROP VIEW dbo.vw_TiposCambioOrkin;
GO
CREATE VIEW dbo.vw_TiposCambioOrkin
AS
WITH Historico AS (
    SELECT TCA_FECHA, TCA_CORDOBAS_USD FROM dbo.TIPO_CAMBIO_HIST
),
Ultimo AS (
    SELECT TOP (1) TCA_CORDOBAS_USD FROM Historico ORDER BY TCA_FECHA DESC
)
SELECT
    CONVERT(varchar(10), TCA_FECHA, 23)  AS Fecha,
    TCA_CORDOBAS_USD                     AS Valor,
    'BANCO_CENTRAL'                      AS FuenteCodigo
FROM Historico
UNION ALL
SELECT
    CONVERT(varchar(10), DATEADD(day, n, Fin), 23),
    (SELECT TCA_CORDOBAS_USD FROM Ultimo),
    'BANCO_CENTRAL'
FROM (
    SELECT MAX(TCA_FECHA) AS Fin FROM Historico
) F
CROSS APPLY (
    SELECT TOP (DATEDIFF(day, F.Fin, CAST(GETDATE() AS date)))
         ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects
) Dias(n)
CROSS APPLY ( SELECT 1 AS x WHERE DATEADD(day, n, F.Fin) <= CAST(GETDATE() AS date) ) Limite;
GO
