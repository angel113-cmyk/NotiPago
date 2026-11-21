-- HT-02: RLS (solo SELECT e INSERT del dueno), sin UPDATE/DELETE, y Realtime.
-- Ejecutar DESPUES de 01_tabla_pagos.sql.

alter table public.pagos enable row level security;
alter table public.pagos force row level security;

-- Limpiar politicas previas para poder re-ejecutar el script.
drop policy if exists pagos_select_propios on public.pagos;
drop policy if exists pagos_insert_propios on public.pagos;

create policy pagos_select_propios
    on public.pagos
    for select
    to authenticated
    using (user_id = auth.uid());

create policy pagos_insert_propios
    on public.pagos
    for insert
    to authenticated
    with check (user_id = auth.uid());

-- No se crean politicas de UPDATE ni DELETE: con RLS activo quedan denegados.
-- Por seguridad extra, quitar tambien los privilegios al rol anon y authenticated.
revoke all on public.pagos from anon;
revoke all on public.pagos from authenticated;
grant select, insert on public.pagos to authenticated;

-- Realtime: agregar la tabla a la publicacion (ignora si ya esta).
do $$
begin
    if not exists (
        select 1 from pg_publication_tables
        where pubname = 'supabase_realtime'
          and schemaname = 'public'
          and tablename = 'pagos'
    ) then
        alter publication supabase_realtime add table public.pagos;
    end if;
end $$;
