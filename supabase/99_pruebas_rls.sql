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

-- Comprobar que no existen politicas de UPDATE/DELETE (debe devolver 0 filas):
select policyname, cmd from pg_policies
where schemaname = 'public' and tablename = 'pagos' and cmd in ('UPDATE', 'DELETE', 'ALL');
