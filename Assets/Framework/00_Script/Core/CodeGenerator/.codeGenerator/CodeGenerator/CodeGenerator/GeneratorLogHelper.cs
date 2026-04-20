using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Text;

namespace CodeGenerator
{
    public static class GeneratorLogHelper
    {
        /// <summary>
        /// 제네레이터 실행 중 터진 Exception을 주석 파일로 생성합니다.
        /// </summary>
        public static void LogError(SourceProductionContext context, string generatorName, Exception ex)
        {
            string errorLog = $"/* \n [{generatorName} 에러 발생!] \n\n {ex} \n */";
        
            // 파일 이름이 겹쳐서 덮어써지지 않도록 고유 ID 부여
            string safeName = $"{generatorName}_Error_{Guid.NewGuid().ToString().Substring(0, 6)}.g.cs";
        
            context.AddSource(safeName, SourceText.From(errorLog, Encoding.UTF8));
        }

        /// <summary>
        /// 단순 텍스트 메시지를 남기고 싶을 때 사용합니다.
        /// </summary>
        public static void LogMessage(SourceProductionContext context, string generatorName, string title, string message)
        {
            string log = $"/* \n [{generatorName} - {title}] \n\n {message} \n */";
            string safeName = $"{generatorName}_{title}_{Guid.NewGuid().ToString().Substring(0, 6)}.g.cs";
        
            context.AddSource(safeName, SourceText.From(log, Encoding.UTF8));
        }
    }
}