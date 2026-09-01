using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;

namespace Project_Star.Entities.Merchants;

// 模板商人：示例商人。
public partial class TemplateMerchant : MerchantBase
{
	public TemplateMerchant()
	{
		AttributeSet = new MerchantAttributeSet();
	}
}
