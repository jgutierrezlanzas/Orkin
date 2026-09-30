-- ═════════════════════════════════════════════════════════════════════════════
--  Clientes del ERP ficticio.
--
--  El cotizador Excel NO tiene hoja de clientes, así que estos no salieron de
--  ningún archivo: son fichas inventadas con el formato que tendría el maestro
--  de clientes de un ERP real (prefijo CLI_, tipos como texto suelto). Existen
--  para poder probar el flujo de carga de CLIENTES de punta a punta.
--
--  Lo que el ETL consume es la VISTA vw_ClientesOrkin: el contrato que traduce
--  las columnas del ERP a los nombres que espera la plataforma.
-- ═════════════════════════════════════════════════════════════════════════════

USE OrkinClienteDemo;
GO

IF OBJECT_ID('dbo.CLIENTE', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CLIENTE
    (
        CLI_CODIGO        nvarchar(20)  NOT NULL CONSTRAINT PK_CLIENTE PRIMARY KEY,
        CLI_NOMBRE        nvarchar(200) NOT NULL,
        CLI_RAZON_SOCIAL  nvarchar(200) NULL,
        CLI_TIPO          nvarchar(20)  NOT NULL,   -- EMPRESARIAL / RESIDENCIAL, como texto suelto
        CLI_RUC           nvarchar(30)  NULL,
        CLI_DIRECCION     nvarchar(300) NULL,
        CLI_CIUDAD        nvarchar(100) NULL,
        CLI_DEPARTAMENTO  nvarchar(100) NULL,
        CLI_TELEFONO      nvarchar(30)  NULL,
        CLI_CORREO        nvarchar(200) NULL,
        CLI_CONTACTO      nvarchar(150) NULL,
        CLI_CARGO         nvarchar(100) NULL,
        CLI_ACTIVO        bit           NOT NULL CONSTRAINT DF_CLIENTE_ACTIVO DEFAULT (1),
        CLI_FECHA_MOD     datetime2     NULL
    );
END
GO

IF OBJECT_ID('dbo.vw_ClientesOrkin', 'V') IS NOT NULL DROP VIEW dbo.vw_ClientesOrkin;
GO

CREATE VIEW dbo.vw_ClientesOrkin
AS
SELECT
    CLI_CODIGO        AS Codigo,
    CLI_NOMBRE        AS Nombre,
    CLI_RAZON_SOCIAL  AS RazonSocial,
    CLI_TIPO          AS TipoClienteCodigo,
    CLI_RUC           AS CedulaORuc,
    CLI_DIRECCION     AS Direccion,
    CLI_CIUDAD        AS Ciudad,
    CLI_DEPARTAMENTO  AS Departamento,
    CLI_TELEFONO      AS Telefono,
    CLI_CORREO        AS Correo,
    CLI_CONTACTO      AS ContactoNombre,
    CLI_CARGO         AS ContactoCargo
FROM dbo.CLIENTE
WHERE CLI_ACTIVO = 1;
GO

DELETE FROM dbo.CLIENTE;
INSERT INTO dbo.CLIENTE (CLI_CODIGO, CLI_NOMBRE, CLI_RAZON_SOCIAL, CLI_TIPO, CLI_RUC, CLI_DIRECCION, CLI_CIUDAD, CLI_DEPARTAMENTO, CLI_TELEFONO, CLI_CORREO, CLI_CONTACTO, CLI_CARGO) VALUES
('CT-0001', 'Hotel Mediterraneo Managua',      'Hotel Mediterraneo S.A.',            'EMPRESARIAL', 'J031-045678-0001A', 'Costado Oeste Rotonda Rubenia',      'Managua',   'Managua',   '+505 2270 1111', 'compras@hotelmediterraneo.ni',  'Ana Ibarra',      'Jefa de Compras'),
('CT-0002', 'Supermercados La Colonia',          'Supermercados La Colonia S.A.',      'EMPRESARIAL', 'J031-098765-0002B', 'Km 4 1/2 Carretera a Masaya',        'Managua',   'Managua',   '+505 2250 2222', 'mantenimiento@lacolonia.ni',    'Luis Talavera',   'Coordinador de Mantenimiento'),
('CT-0003', 'Carnicos del Norte',                 'Carnicos del Norte S.A.',            'EMPRESARIAL', 'J031-011223-0003C', 'Zona Industrial Sandino, Nave 4',    'Esteli',    'Esteli',    '+505 2713 3333', 'calidad@carnicosnorte.ni',      'Marta Cruz',      'Gerente de Calidad'),
('CT-0004', 'Panaderia El Buen Sabor',            NULL,                                 'EMPRESARIAL', '0010-050678-1122K', 'Del Parque Central 1 cuadra al sur', 'Masaya',    'Masaya',    '+505 2522 4444', 'panaderiaelbuensabor@gmail.com', 'Carlos Mejia',  'Propietario'),
('CT-0005', 'Juan Perez Vega',                    NULL,                                 'RESIDENCIAL', '0011-070889-3311L', 'Residencial Las Colinas, Casa 12',   'Managua',   'Managua',   '+505 8888 0101', 'juanperezvega@mail.com',        NULL,              NULL),
('CT-0006', 'Restaurant El Bacanal',              'Bacanal Enterprises S.A.',           'EMPRESARIAL', 'J031-022344-0004D', 'Paseo Xolotlan, Local 7',             'Managua',   'Managua',   '+505 2266 5555', 'admin@elbacanal.ni',            'Rosa Chavez',     'Administradora'),
('CT-0007', 'Almacenadora Pacifico',              'Almacenadora del Pacifico S.A.',     'EMPRESARIAL', 'J031-055667-0005E', 'Puerto Salvador Allende, Bodega 3',   'Managua',   'Managua',   '+505 2289 6666', 'operaciones@almacenadorapacifico.ni', 'Jorge Mendoza', 'Jefe de Operaciones'),
('CT-0008', 'Maria Jose Lopez Rivas',             NULL,                                 'RESIDENCIAL', '0012-090123-4455M', 'Colonia Centroamerica, Casa 45',      'Leon',      'Leon',      '+505 8888 0202', 'majolopez87@mail.com',          NULL,              NULL),
('CT-0009', 'Fabrica de Confites Dulcinea',       'Dulcinea Confites S.A.',             'EMPRESARIAL', 'J031-077889-0006F', 'Parque Industrial Cuidadela, Nave 9', 'Granada',   'Granada',   '+505 2552 7777', 'produccion@dulcinea.ni',       'Pedro Altamirano','Jefe de Produccion'),
('CT-0010', 'Colegio San Ramon',                   'Fundacion Educativa San Ramon',     'EMPRESARIAL', 'J031-033445-0007G', 'Barrio San Ramon, del Stadium 2c Este','Managua',  'Managua',   '+505 2249 8888', 'administracion@sanramon.edu.ni','Silvia Pineda', 'Administradora'),
('CT-0011', 'Clinica Veterinaria Huellas',         'Huellas Veterinaria S.A.',          'EMPRESARIAL', 'J031-044556-0008H', 'Del Semaforo El Gueguense 300 m al sur','Managua', 'Managua',   '+505 2277 9999', 'citas@huellas.ni',              'Gabriel Rios',    'Director Tecnico'),
('CT-0012', 'Distribuidora Fuentes del Norte',    'Fuentes del Norte S.A.',             'EMPRESARIAL', 'J031-066778-0009I', 'Carretera Norte Km 12, Plaza Comercial','Managua',  'Managua',   '+505 2630 1010', 'ventas@fuentesnorte.ni',       'Elena Sotomayor', 'Gerente de Ventas'),
('CT-0013', 'Roberto Antonio Aguilar',            NULL,                                 'RESIDENCIAL', '0013-012345-6677N', 'Barrio San Judas, Casa 23',           'Chinandega','Chinandega','+505 8888 0303', 'roberto.aguilar1975@mail.com', NULL,             NULL),
('CT-0014', 'Hotel Casa del Sol',                  'Hospitalidad Casa del Sol S.A.',     'EMPRESARIAL', 'J031-088990-0010J', 'Frente al Malecon, Calle La Calzada', 'Granada',   'Granada',   '+505 2558 1110', 'gerencia@casadelsol.ni',       'Nora Belli',      'Gerente General'),
('CT-0015', 'Procesadora de Cafe Alturas',         'Alturas Coffee S.A.',                'EMPRESARIAL', 'J031-099001-0011K', 'Km 145 Carretera Norte, Matagalpa',  'Matagalpa', 'Matagalpa', '+505 2772 1212', 'sac@alturascoffee.ni',          'Oscar Zeledon',   'Supervisor de Aseguramiento'),
('CT-0016', 'Gimnasio Olympia Fitness',           'Olympia Fitness S.A.',               'EMPRESARIAL', 'J031-010112-0012L', 'Metrocentro, 2do piso',               'Managua',   'Managua',   '+505 2270 1313', 'info@olympiafitness.ni',       'Diana Urcuyo',    'Encargada de Instalaciones');
GO
