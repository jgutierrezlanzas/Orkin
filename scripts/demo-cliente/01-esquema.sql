-- ═════════════════════════════════════════════════════════════════════════════
--  BD ficticia del cliente para simular el ETL de integración (Fase 12).
--
--  La idea NO es replicar nuestro esquema, sino la de un ERP real de ORKIN:
--  los datos viven distribuidos en varias tablas, con nombres de columnas y
--  formatos propios del ERP (unidades como 'ml', 'Litro', 'H.H', 'lb', ...).
--  D-19: esta BD se lee SOLO LECTURA desde el cotizador.
--
--  Lo que el ETL consume es la VISTA de origen (vw_ProductosOrkin, ...): el
--  cliente no tiene que cambiar NADA de sus tablas. La vista es el contrato
--  entre el ERP y nuestro cargador.
-- ═════════════════════════════════════════════════════════════════════════════

IF DB_ID('OrkinClienteDemo') IS NULL
BEGIN
    CREATE DATABASE OrkinClienteDemo;
END
GO

USE OrkinClienteDemo;
GO

-- ── Tabla de unidades del ERP ────────────────────────────────────────────────
-- El ERP maneja sus propias claves: ml, Litro, H.H (horas hombre), lb (libra),
-- y empaques (Bolsa, Rollo, Yarda, Par). Nada que ver con nuestro catálogo.
IF OBJECT_ID('dbo.UNIDAD', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UNIDAD
    (
        UNI_CODIGO  nvarchar(10)  NOT NULL CONSTRAINT PK_UNIDAD PRIMARY KEY,
        UNI_NOMBRE  nvarchar(50)  NOT NULL,
        UNI_ACTIVO  bit           NOT NULL CONSTRAINT DF_UNIDAD_ACTIVO DEFAULT (1)
    );
END
GO

-- ── Familias / líneas de producto del ERP ────────────────────────────────────
IF OBJECT_ID('dbo.GRUPO', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.GRUPO
    (
        GRU_CODIGO  nvarchar(20)  NOT NULL CONSTRAINT PK_GRUPO PRIMARY KEY,
        GRU_NOMBRE  nvarchar(80)  NOT NULL,
        GRU_ACTIVO  bit           NOT NULL CONSTRAINT DF_GRUPO_ACTIVO DEFAULT (1)
    );
END
GO

-- ── Maestro de artículos ─────────────────────────────────────────────────────
-- El maestro. La unidad y la familia son FK a otras tablas: el dato relevante
-- para el cotizador está repartido entre ARTICULO + UNIDAD + GRUPO.
IF OBJECT_ID('dbo.ARTICULO', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ARTICULO
    (
        ART_CODIGO          nvarchar(50)   NOT NULL CONSTRAINT PK_ARTICULO PRIMARY KEY,
        ART_NOMBRE          nvarchar(120)  NOT NULL,
        ART_DESCRIPCION     nvarchar(250)  NULL,
        ART_UNIDAD_CODIGO   nvarchar(10)   NOT NULL,
        ART_GRUPO_CODIGO    nvarchar(20)   NULL,
        ART_COSTO           decimal(18,6)  NOT NULL,
        ART_PRECIO_VENTA    decimal(18,6)  NULL,
        ART_VIDA_UTIL_MESES int            NULL,
        ART_ACTIVO          bit            NOT NULL CONSTRAINT DF_ARTICULO_ACTIVO DEFAULT (1),
        ART_FECHA_MOD       datetime2(3)   NOT NULL CONSTRAINT DF_ARTICULO_FECHA DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_ARTICULO_UNIDAD FOREIGN KEY (ART_UNIDAD_CODIGO)
            REFERENCES dbo.UNIDAD (UNI_CODIGO),
        CONSTRAINT FK_ARTICULO_GRUPO FOREIGN KEY (ART_GRUPO_CODIGO)
            REFERENCES dbo.GRUPO (GRU_CODIGO)
    );
END
GO

-- ── Histórico del tipo de cambio (BCN) ───────────────────────────────────────
IF OBJECT_ID('dbo.TIPO_CAMBIO_HIST', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TIPO_CAMBIO_HIST
    (
        TCA_FECHA         date           NOT NULL CONSTRAINT PK_TCA PRIMARY KEY,
        TCA_CORDOBAS_USD  decimal(18,6)  NOT NULL
    );
END
GO

-- ── Tipos de servicio que ofrece ORKIN ───────────────────────────────────────
IF OBJECT_ID('dbo.TIPO_SERVICIO', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TIPO_SERVICIO
    (
        SVC_CODIGO  nvarchar(20)  NOT NULL CONSTRAINT PK_SVC PRIMARY KEY,
        SVC_NOMBRE  nvarchar(80)  NOT NULL,
        SVC_ACTIVO  bit           NOT NULL CONSTRAINT DF_SVC_ACTIVO DEFAULT (1)
    );
END
GO

-- ═════════════════════════════════════════════════════════════════════════════
--  VISTA DE ORIGEN: el contrato entre el ERP y nuestro cargador.
--
--  Une ARTICULO + UNIDAD + GRUPO y entrega las columnas que nuestro
--  CargadorProductos espera (Codigo, Nombre, Descripcion, CategoriaCodigo,
--  UnidadMedidaCodigo, CostoActual, PrecioVentaActual, VidaUtilMeses).
--
--  La limpieza y la normalización viven AQUÍ (en el "origen"): los códigos de
--  unidad del ERP se traducen a los nuestros. El ERP no cambia nada; el que se
--  adapta es el contrato, configurado como "origen" del flujo ETL.
-- ═════════════════════════════════════════════════════════════════════════════
IF OBJECT_ID('dbo.vw_ProductosOrkin', 'V') IS NOT NULL
    DROP VIEW dbo.vw_ProductosOrkin;
GO

CREATE VIEW dbo.vw_ProductosOrkin AS
SELECT
    A.ART_CODIGO                              AS Codigo,
    A.ART_NOMBRE                              AS Nombre,
    COALESCE(A.ART_DESCRIPCION, A.ART_NOMBRE) AS Descripcion,
    G.GRU_CODIGO                              AS CategoriaCodigo,
    CASE A.ART_UNIDAD_CODIGO
        WHEN 'ml'    THEN 'ML'
        WHEN 'Litro' THEN 'LT'
        WHEN 'l'     THEN 'LT'
        WHEN 'und'   THEN 'UND'
        WHEN 'gr'    THEN 'GR'
        WHEN 'km'    THEN 'KM'
        WHEN 'H.H'   THEN 'HH'
        WHEN 'lb'    THEN 'LB'
        WHEN 'Bolsa' THEN 'BOLSA'
        WHEN 'Rollo' THEN 'ROLLO'
        WHEN 'Yarda' THEN 'YARDA'
        WHEN 'Par'   THEN 'PAR'
        ELSE 'UND'
    END                                       AS UnidadMedidaCodigo,
    A.ART_COSTO                               AS CostoActual,
    ISNULL(A.ART_PRECIO_VENTA, A.ART_COSTO)   AS PrecioVentaActual,
    A.ART_VIDA_UTIL_MESES                     AS VidaUtilMeses
FROM dbo.ARTICULO A
LEFT JOIN dbo.UNIDAD U ON U.UNI_CODIGO = A.ART_UNIDAD_CODIGO
LEFT JOIN dbo.GRUPO  G ON G.GRU_CODIGO = A.ART_GRUPO_CODIGO
WHERE A.ART_ACTIVO = 1;
GO