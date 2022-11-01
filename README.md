# 401_shpilevoy
Георгий Шпилевой - лабораторные работы


library - библиотека со встроенным ресурсом .onnx
application  - пример асснихроного импользования компонента

Перед упаковкой компонента нужно положить в директорию library файлс предобученной моделью

# скачать модель можно [здесь](https://github.com/onnx/models/blob/main/vision/body_analysis/arcface/model/arcfaceresnet100-8.onnx)

Команда для установки пакета из директории library
```shell
dotnet pack .
```

Команда для добавления пакета в зависимости
```shell
dotnet add package Georgy.Component -v 0.0.0.10
```


