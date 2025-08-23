using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Loco.Core.Services;

namespace Loco.Tools
{
    /// <summary>
    /// Generate localization files for all supported languages
    /// </summary>
    class LocalizationGenerator
    {
        private static readonly Dictionary<string, Dictionary<string, string>> BaseTranslations = new()
        {
            ["en"] = new()
            {
                ["app.name"] = "Loco",
                ["app.description"] = "Automation Platform",
                ["general.yes"] = "Yes",
                ["general.no"] = "No",
                ["general.ok"] = "OK",
                ["general.cancel"] = "Cancel",
                ["general.save"] = "Save",
                ["general.load"] = "Load",
                ["general.delete"] = "Delete",
                ["general.edit"] = "Edit",
                ["general.add"] = "Add",
                ["general.remove"] = "Remove",
                ["status.running"] = "Running",
                ["status.stopped"] = "Stopped",
                ["status.error"] = "Error",
                ["status.success"] = "Success"
            },
            ["ja"] = new()
            {
                ["app.name"] = "Loco",
                ["app.description"] = "自動化プラットフォーム",
                ["general.yes"] = "はい",
                ["general.no"] = "いいえ",
                ["general.ok"] = "OK",
                ["general.cancel"] = "キャンセル",
                ["general.save"] = "保存",
                ["general.load"] = "読み込み",
                ["general.delete"] = "削除",
                ["general.edit"] = "編集",
                ["general.add"] = "追加",
                ["general.remove"] = "削除",
                ["status.running"] = "実行中",
                ["status.stopped"] = "停止",
                ["status.error"] = "エラー",
                ["status.success"] = "成功"
            },
            ["zh"] = new()
            {
                ["app.name"] = "Loco",
                ["app.description"] = "自动化平台",
                ["general.yes"] = "是",
                ["general.no"] = "否",
                ["general.ok"] = "确定",
                ["general.cancel"] = "取消",
                ["general.save"] = "保存",
                ["general.load"] = "加载",
                ["general.delete"] = "删除",
                ["general.edit"] = "编辑",
                ["general.add"] = "添加",
                ["general.remove"] = "移除",
                ["status.running"] = "运行中",
                ["status.stopped"] = "已停止",
                ["status.error"] = "错误",
                ["status.success"] = "成功"
            },
            ["es"] = new()
            {
                ["app.name"] = "Loco",
                ["app.description"] = "Plataforma de Automatización",
                ["general.yes"] = "Sí",
                ["general.no"] = "No",
                ["general.ok"] = "OK",
                ["general.cancel"] = "Cancelar",
                ["general.save"] = "Guardar",
                ["general.load"] = "Cargar",
                ["general.delete"] = "Eliminar",
                ["general.edit"] = "Editar",
                ["general.add"] = "Añadir",
                ["general.remove"] = "Quitar",
                ["status.running"] = "Ejecutando",
                ["status.stopped"] = "Detenido",
                ["status.error"] = "Error",
                ["status.success"] = "Éxito"
            },
            ["fr"] = new()
            {
                ["app.name"] = "Loco",
                ["app.description"] = "Plateforme d'Automatisation",
                ["general.yes"] = "Oui",
                ["general.no"] = "Non",
                ["general.ok"] = "OK",
                ["general.cancel"] = "Annuler",
                ["general.save"] = "Enregistrer",
                ["general.load"] = "Charger",
                ["general.delete"] = "Supprimer",
                ["general.edit"] = "Modifier",
                ["general.add"] = "Ajouter",
                ["general.remove"] = "Retirer",
                ["status.running"] = "En cours",
                ["status.stopped"] = "Arrêté",
                ["status.error"] = "Erreur",
                ["status.success"] = "Succès"
            },
            ["de"] = new()
            {
                ["app.name"] = "Loco",
                ["app.description"] = "Automatisierungsplattform",
                ["general.yes"] = "Ja",
                ["general.no"] = "Nein",
                ["general.ok"] = "OK",
                ["general.cancel"] = "Abbrechen",
                ["general.save"] = "Speichern",
                ["general.load"] = "Laden",
                ["general.delete"] = "Löschen",
                ["general.edit"] = "Bearbeiten",
                ["general.add"] = "Hinzufügen",
                ["general.remove"] = "Entfernen",
                ["status.running"] = "Läuft",
                ["status.stopped"] = "Gestoppt",
                ["status.error"] = "Fehler",
                ["status.success"] = "Erfolg"
            },
            ["ko"] = new()
            {
                ["app.name"] = "Loco",
                ["app.description"] = "자동화 플랫폼",
                ["general.yes"] = "예",
                ["general.no"] = "아니오",
                ["general.ok"] = "확인",
                ["general.cancel"] = "취소",
                ["general.save"] = "저장",
                ["general.load"] = "불러오기",
                ["general.delete"] = "삭제",
                ["general.edit"] = "편집",
                ["general.add"] = "추가",
                ["general.remove"] = "제거",
                ["status.running"] = "실행 중",
                ["status.stopped"] = "중지됨",
                ["status.error"] = "오류",
                ["status.success"] = "성공"
            },
            ["pt"] = new()
            {
                ["app.name"] = "Loco",
                ["app.description"] = "Plataforma de Automação",
                ["general.yes"] = "Sim",
                ["general.no"] = "Não",
                ["general.ok"] = "OK",
                ["general.cancel"] = "Cancelar",
                ["general.save"] = "Salvar",
                ["general.load"] = "Carregar",
                ["general.delete"] = "Excluir",
                ["general.edit"] = "Editar",
                ["general.add"] = "Adicionar",
                ["general.remove"] = "Remover",
                ["status.running"] = "Executando",
                ["status.stopped"] = "Parado",
                ["status.error"] = "Erro",
                ["status.success"] = "Sucesso"
            },
            ["ru"] = new()
            {
                ["app.name"] = "Loco",
                ["app.description"] = "Платформа автоматизации",
                ["general.yes"] = "Да",
                ["general.no"] = "Нет",
                ["general.ok"] = "ОК",
                ["general.cancel"] = "Отмена",
                ["general.save"] = "Сохранить",
                ["general.load"] = "Загрузить",
                ["general.delete"] = "Удалить",
                ["general.edit"] = "Редактировать",
                ["general.add"] = "Добавить",
                ["general.remove"] = "Удалить",
                ["status.running"] = "Выполняется",
                ["status.stopped"] = "Остановлено",
                ["status.error"] = "Ошибка",
                ["status.success"] = "Успех"
            },
            ["ar"] = new()
            {
                ["app.name"] = "Loco",
                ["app.description"] = "منصة الأتمتة",
                ["general.yes"] = "نعم",
                ["general.no"] = "لا",
                ["general.ok"] = "موافق",
                ["general.cancel"] = "إلغاء",
                ["general.save"] = "حفظ",
                ["general.load"] = "تحميل",
                ["general.delete"] = "حذف",
                ["general.edit"] = "تحرير",
                ["general.add"] = "إضافة",
                ["general.remove"] = "إزالة",
                ["status.running"] = "قيد التشغيل",
                ["status.stopped"] = "متوقف",
                ["status.error"] = "خطأ",
                ["status.success"] = "نجاح"
            }
        };

        public static async Task GenerateAllLanguageFiles(string outputPath)
        {
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            var languages = LanguageManager.GetSupportedLanguageCodes();
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            foreach (var lang in languages)
            {
                Dictionary<string, string> translations;
                
                // Use existing translations or fall back to English
                if (BaseTranslations.ContainsKey(lang))
                {
                    translations = BaseTranslations[lang];
                }
                else
                {
                    // Copy English and add language name
                    translations = new Dictionary<string, string>(BaseTranslations["en"]);
                    translations["language.name"] = LanguageManager.GetLanguageName(lang);
                    translations["language.code"] = lang;
                }

                var filePath = Path.Combine(outputPath, $"{lang}.json");
                var json = JsonSerializer.Serialize(translations, options);
                await File.WriteAllTextAsync(filePath, json);
                
                Console.WriteLine($"Generated: {filePath}");
            }

            Console.WriteLine($"\nGenerated {languages.Count()} language files in {outputPath}");
        }

        static async Task Main(string[] args)
        {
            var outputPath = args.Length > 0 
                ? args[0] 
                : Path.Combine(AppContext.BaseDirectory, "locales");
                
            await GenerateAllLanguageFiles(outputPath);
        }
    }
}
