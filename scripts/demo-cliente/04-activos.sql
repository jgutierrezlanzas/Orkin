-- ═════════════════════════════════════════════════════════════════════════════
--  Activos amortizables del ERP ficticio.
--
--  El Excel de ORKIN trae 19 activos en Tabla9 (código→nombre→precio). Estos son
--  los que la cotización usa como "amortizables": trampas, lámparas UV, cebaderos,
--  equipos que se instalan y se descuentan por meses, no de golpe.
--
--  La vista de contrato vw_ActivosOrkin es lo que el ETL consume.
-- ═════════════════════════════════════════════════════════════════════════════

USE OrkinClienteDemo;
GO

IF OBJECT_ID('dbo.ACTIVO', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ACTIVO (
        ACT_CODIGO        nvarchar(20)  NOT NULL CONSTRAINT PK_ACTIVO PRIMARY KEY,
        ACT_NOMBRE        nvarchar(200) NOT NULL,
        ACT_COSTO         decimal(18,6) NOT NULL,
        ACT_VIDA_UTIL_MESES int          NOT NULL,
        ACT_UNIDAD_CODIGO  nvarchar(10)  NOT NULL,
        ACT_GRUPO_CODIGO   nvarchar(20)  NULL,
        ACT_VALOR_RESIDUAL decimal(18,6) NOT NULL CONSTRAINT DF_ACT_RESIDUAL DEFAULT (0),
        ACT_CANTIDAD       int           NOT NULL CONSTRAINT DF_ACT_CANT DEFAULT (1),
        ACT_ACTIVO          bit           NOT NULL CONSTRAINT DF_ACT_ACTIVO DEFAULT (1)
    );
END
GO

IF OBJECT_ID('dbo.vw_ActivosOrkin', 'V') IS NOT NULL DROP VIEW dbo.vw_ActivosOrkin;
GO

CREATE VIEW dbo.vw_ActivosOrkin
AS
SELECT
    ACT_CODIGO          AS Codigo,
    ACT_NOMBRE          AS Nombre,
    NULL                AS Descripcion,
    ACT_GRUPO_CODIGO    AS CategoriaCodigo,
    ACT_UNIDAD_CODIGO   AS UnidadMedidaCodigo,
    ACT_COSTO           AS CostoActual,
    ACT_VIDA_UTIL_MESES AS MesesVidaUtil,
    ACT_VALOR_RESIDUAL  AS ValorResidual,
    ACT_CANTIDAD        AS Cantidad,
    ROW_NUMBER() OVER (ORDER BY ACT_CODIGO) AS Orden
FROM dbo.ACTIVO
WHERE ACT_ACTIVO = 1;
GO

DELETE FROM dbo.ACTIVO;
INSERT INTO dbo.ACTIVO (ACT_CODIGO, ACT_NOMBRE, ACT_COSTO, ACT_VIDA_UTIL_MESES, ACT_UNIDAD_CODIGO, ACT_GRUPO_CODIGO) VALUES
('ACT-001', 'LAMPARA GENUS ECLIPSE ULTRA 2 TUBOS',   1180.00, 24, 'und', 'EP'),
('ACT-002', 'LAMPARA GENUS ECLIPSE 1 TUBO',          680.00, 24, 'und', 'EP'),
('ACT-003', 'BUJIA MANTIS 1X2 ACERO INOXIDABLE',      210.00, 12, 'und', 'EP'),
('ACT-004', 'BUJIA HALO 1X2',                         180.00, 12, 'und', 'EP'),
('ACT-005', 'PORTACEBOS CQM RATA (EL COLONO)',        140.00, 12, 'und', 'MT'),
('ACT-006', 'CEBADERO PARA RATAS ORNAMENTAL',         240.00, 12, 'und', 'MT'),
('ACT-007', 'CEBADERO PARA RATAS',                   410.00, 12, 'und', 'MT'),
('ACT-008', 'TRAMPA DE CAPTURA VIVA PARA GATOS',      350.00, 36, 'und', 'MT'),
('ACT-009', 'TRAMPA GOMOSA VICTOR',                   120.00,  6, 'und', 'MT'),
('ACT-010', 'BANDEJA GOMOSA JAWZ',                    95.00,  6, 'und', 'MT'),
('ACT-011', 'TRAMPA PEGAJOSA PARA RATON - MAXTRAX',   115.00,  6, 'und', 'MT'),
('ACT-012', 'TRAMPAS PARA MOSCAS',                    260.00, 12, 'und', 'MT'),
('ACT-013', 'TRAMPAS PARA MOSCAS CON FEROMONAS',     310.00, 12, 'und', 'MT'),
('ACT-014', 'DESNUCADORA GRANDE (JAWZ)',              420.00, 24, 'und', 'MT'),
('ACT-015', 'DESNUCADORA PLASTICA PEQUEÑA',           180.00, 24, 'und', 'MT'),
('ACT-016', 'ESTACION DE MONITOREO DE TERMITAS',      850.00, 36, 'und', 'MT'),
('ACT-017', 'TRANSONIC PRO/AUYENTADOR SONICO',        890.00, 24, 'und', 'EP'),
('ACT-018', 'AHUYENTADOR REFLECTIVO DE AVES',        150.00, 12, 'und', 'EP'),
('ACT-019', 'REPUESTOS DE MADERA PARA ESTACIONES',     95.00,  6, 'und', 'MT');
GO
