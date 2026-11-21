-- Prueba de HT-02: sin sesion no se lee ni se escribe nada.
-- Ejecutar en el SQL Editor (corre como postgres, por eso simulamos el rol anon).

begin;
set local role anon;

-- Debe fallar con "permission denied for table pagos".
do $$
begin
    begin
        perform count(*) from public.pagos;
        raise exception 'FALLO: anon pudo leer pagos';
    exception when insufficient_privilege then
        raise notice 'OK: anon NO puede leer';
    end;

    begin
        insert into public.pagos (user_id, monto, fecha_hora, hash)
        values (gen_random_uuid(), 1, now(), 'prueba');
        raise exception 'FALLO: anon pudo insertar pagos';
    exception when insufficient_privilege then
        raise notice 'OK: anon NO puede insertar';
    end;
end $$;

rollback;

-- Comprobar politicas que NO sean de lectura/insercion.
-- Resultado esperado: 0 filas antes de ejecutar 04_es_prueba.sql, o SOLO
-- 'pagos_delete_pruebas' (DELETE) despues. Cualquier UPDATE o ALL seria un error.
select policyname, cmd from pg_policies
where schemaname = 'public' and tablename = 'pagos' and cmd in ('UPDATE', 'DELETE', 'ALL');
