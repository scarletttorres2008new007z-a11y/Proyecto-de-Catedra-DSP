# 9. Subir el proyecto a GitHub

Antes de subir, compruebe que no haya secretos:

```bash
git status                       # no deben aparecer bin/, obj/, App_Data/, Logs/, *.bak ni *.db (están en .gitignore)
git grep -n -i "password=" -- ':!docs'   # no debe mostrar contraseñas reales
```

Las cadenas de conexión con contraseña y las credenciales SMTP van en User Secrets, que se guardan fuera del proyecto.

```bash
git add -A
git commit -m "Descripción del cambio"
git push -u origin <rama>
```

Luego abra un Pull Request en GitHub hacia la rama principal y revise los archivos antes de fusionar.
