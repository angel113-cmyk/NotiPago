# HT-03: usuario admin y registro cerrado (pasos manuales)

Se hace en el panel de Supabase, no hay script. Nunca uses ni compartas la `service_role`.

## 1. Crear el admin
1. Panel del proyecto -> **Authentication -> Users -> Add user -> Create new user**.
2. Correo: el de la tienda. Contrasena: larga y unica (guardala en un gestor).
3. Marca **Auto Confirm User** para que no pida confirmar el correo.

## 2. Desactivar el registro publico
1. **Authentication -> Sign In / Providers** (o *Configuration -> Sign In / Providers*).
2. Apaga **Allow new users to sign up**.
3. Deja habilitado solo el proveedor **Email**. Desactiva los demas (anonymous, phone, etc.).
4. Guarda.

## 3. Comprobar
- Un registro nuevo desde la API devuelve error "Signups not allowed".
- El login con el admin devuelve un `access_token`.

## 4. Claves que usan las apps
*Project Settings -> API Keys*: solo la **publishable (anon)** va en `appsettings.json`.
La contrasena de la base de datos y la `secret/service_role` no van en ninguna app.
